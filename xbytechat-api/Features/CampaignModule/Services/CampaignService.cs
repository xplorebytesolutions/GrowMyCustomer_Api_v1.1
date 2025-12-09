
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Shared;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Services.Messages.Interfaces;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Helpers;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Shared.utility;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Features.CTAFlowBuilder.Models;

using xbytechat.api.WhatsAppSettings.Services;
using xbytechat_api.Features.Billing.Services;
using System.Text.RegularExpressions;
using xbytechat.api.Common.Utils;
using xbytechat.api.Features.TemplateModule.Services;
using System.Linq;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CampaignModule.Services.SendPipeline;
using System.Text.Json;
using Newtonsoft.Json;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public partial class CampaignService : ICampaignService
    {
        private readonly AppDbContext _context;
        private readonly IMessageService _messageService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILeadTimelineService _timelineService;
        private readonly IMessageEngineService _messageEngineService;
        private readonly IWhatsAppTemplateFetcherService _templateFetcherService;
        private readonly IUrlBuilderService _urlBuilderService;
        private readonly IWhatsAppSenderService _whisatsAppSenderService;
        private readonly IBillingIngestService _billingIngest;

        private readonly IWhatsAppSettingsService _whatsAppSettingsService;
        // private readonly Serilog.ILogger _logger = Log.ForContext<CampaignService>();
        // CampaignService.cs (fields)


        private readonly ILogger<WhatsAppTemplateService> _logger;
        public CampaignService(AppDbContext context, IMessageService messageService,
                               IServiceProvider serviceProvider,
                               ILeadTimelineService timelineService,
                               IMessageEngineService messageEngineService,
                               IWhatsAppTemplateFetcherService templateFetcherService,
                               IUrlBuilderService urlBuilderService,
                               IWhatsAppSenderService whatsAppSenderService, IBillingIngestService billingIngest,
                               ILogger<WhatsAppTemplateService> logger, IWhatsAppSettingsService whatsAppSettingsService
                               )
        {
            _context = context;
            _messageService = messageService;
            _serviceProvider = serviceProvider;
            _timelineService = timelineService; // ✅ new
            _messageEngineService = messageEngineService;
            _templateFetcherService = templateFetcherService;
            _urlBuilderService = urlBuilderService;
            _whisatsAppSenderService = whatsAppSenderService;
            _billingIngest = billingIngest;
            _logger = logger;
            _whatsAppSettingsService = whatsAppSettingsService;


        }




        #region Private method
        private static string? ResolvePerRecipientValue(CampaignRecipient r, string key)
        {
            if (string.IsNullOrWhiteSpace(r.ResolvedButtonUrlsJson)) return null;
            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(r.ResolvedButtonUrlsJson)
                           ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return dict.TryGetValue(key, out var v) ? v : null;
            }
            catch { return null; }
        }
        private static List<string> BuildBodyParametersForRecipient(Campaign campaign, CampaignRecipient r)
        {
            // Preferred: frozen params on recipient (string[])
            if (!string.IsNullOrWhiteSpace(r.ResolvedParametersJson))
            {
                try
                {
                    var arr = JsonConvert.DeserializeObject<string[]>(r.ResolvedParametersJson);
                    if (arr != null) return arr.ToList();
                }
                catch { /* ignore */ }
            }

            // Fallback: campaign.TemplateParameters (stored as JSON array of strings)
            try
            {
                return TemplateParameterHelper.ParseTemplateParams(campaign.TemplateParameters).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
        private static Dictionary<string, string> BuildButtonParametersForRecipient(Campaign campaign, CampaignRecipient r)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1) Recipient-specific vars (from CSV materialization)
            if (!string.IsNullOrWhiteSpace(r.ResolvedButtonUrlsJson))
            {
                try
                {
                    var d = JsonConvert.DeserializeObject<Dictionary<string, string>>(r.ResolvedButtonUrlsJson);
                    if (d != null)
                    {
                        foreach (var kv in d)
                            dict[kv.Key] = kv.Value ?? string.Empty;
                    }
                }
                catch { /* ignore */ }
            }

            // 2) Header fallbacks from campaign (only ImageUrl exists in this branch)
            if (!dict.ContainsKey("header.image_url") && !string.IsNullOrWhiteSpace(campaign.ImageUrl))
                dict["header.image_url"] = campaign.ImageUrl!;

            // NOTE:
            // We do NOT touch header.video_url/header.document_url here,
            // because Campaign.VideoUrl/DocumentUrl do not exist in this branch.

            // 3) Button URL fallbacks from campaign buttons
            if (campaign.MultiButtons != null)
            {
                foreach (var b in campaign.MultiButtons.OrderBy(b => b.Position).Take(3))
                {
                    var key = $"button{b.Position}.url_param";
                    if (!dict.ContainsKey(key) && !string.IsNullOrWhiteSpace(b.Value))
                        dict[key] = b.Value!;
                }
            }

            return dict;
        }
        private async Task<(Guid? entryStepId, string? entryTemplate)> ResolveFlowEntryAsync(Guid businessId, Guid? flowId)
        {
            if (!flowId.HasValue || flowId.Value == Guid.Empty) return (null, null);

            var flow = await _context.CTAFlowConfigs
                 .AsNoTracking()
                .Include(f => f.Steps)
                    .ThenInclude(s => s.ButtonLinks)
                .FirstOrDefaultAsync(f => f.Id == flowId.Value && f.BusinessId == businessId && f.IsActive);

            if (flow == null || flow.Steps == null || flow.Steps.Count == 0) return (null, null);

            var incoming = new HashSet<Guid>(
                flow.Steps.SelectMany(s => s.ButtonLinks)
                          .Where(l => l.NextStepId.HasValue)
                          .Select(l => l.NextStepId!.Value)
            );

            var entry = flow.Steps
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault(s => !incoming.Contains(s.Id));

            return entry == null ? (null, null) : (entry.Id, entry.TemplateToSend);
        }

        #endregion

        #region Get All Types of Get and Update and Delete Methods

        public async Task<List<CampaignSummaryDto>> GetAllCampaignsAsync(Guid businessId)
        {
            return await _context.Campaigns
                .Where(c => c.BusinessId == businessId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CampaignSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Status = c.Status,
                    ScheduledAt = c.ScheduledAt,
                    CreatedAt = c.CreatedAt,

                })
                .ToListAsync();
        }
        public async Task<CampaignDto?> GetCampaignByIdAsync(Guid campaignId, Guid businessId)
        {
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Include(c => c.Cta)
                .Include(c => c.MultiButtons)
                .Include(c => c.CTAFlowConfig)
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

            if (campaign == null) return null;

            return new CampaignDto
            {
                Id = campaign.Id,
                Name = campaign.Name,
                MessageTemplate = campaign.MessageTemplate,
                MessageBody = campaign.MessageBody,
                TemplateId = campaign.TemplateId,
                CampaignType = campaign.CampaignType,
                Status = campaign.Status,
                ImageUrl = campaign.ImageUrl,
                ImageCaption = campaign.ImageCaption,
                CreatedAt = campaign.CreatedAt,
                ScheduledAt = campaign.ScheduledAt,
                CtaId = campaign.CtaId,
                Cta = campaign.Cta == null ? null : new CtaPreviewDto
                {
                    Title = campaign.Cta.Title,
                    ButtonText = campaign.Cta.ButtonText
                },
                MultiButtons = campaign.MultiButtons?
                    .Select(b => new CampaignButtonDto
                    {
                        ButtonText = b.Title,
                        ButtonType = b.Type,
                        TargetUrl = b.Value
                    }).ToList() ?? new List<CampaignButtonDto>(),
                // ✅ Flow surface to UI
                CTAFlowConfigId = campaign.CTAFlowConfigId,
                CTAFlowName = campaign.CTAFlowConfig?.FlowName
            };
        }
        // Returns the entry step (no incoming links) and its template name.
        // If flow is missing/invalid, returns (null, null) and caller should ignore.

        public async Task<List<CampaignSummaryDto>> GetAllCampaignsAsync(Guid businessId, string? type = null)
        {
            var query = _context.Campaigns
                .Where(c => c.BusinessId == businessId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(type))
                query = query.Where(c => c.CampaignType == type);

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CampaignSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Status = c.Status,
                    ScheduledAt = c.ScheduledAt,
                    CreatedAt = c.CreatedAt,
                    ImageUrl = c.ImageUrl,            // ✅ Now mapped
                    ImageCaption = c.ImageCaption,    // ✅ Now mapped
                    CtaTitle = c.Cta != null ? c.Cta.Title : null,  // optional
                    RecipientCount = c.Recipients.Count()
                })
                .ToListAsync();
        }

        public async Task<List<ContactDto>> GetRecipientsByCampaignIdAsync(Guid campaignId, Guid businessId)
        {
            var recipients = await _context.CampaignRecipients
                .Include(r => r.Contact)
                .Where(r => r.CampaignId == campaignId && r.Contact.BusinessId == businessId)
                .Select(r => new ContactDto
                {
                    Id = r.Contact.Id,
                    Name = r.Contact.Name,
                    PhoneNumber = r.Contact.PhoneNumber,
                    Email = r.Contact.Email,
                    LeadSource = r.Contact.LeadSource,
                    CreatedAt = r.Contact.CreatedAt
                })
                .ToListAsync();

            return recipients;
        }

        public async Task<PaginatedResponse<CampaignSummaryDto>> GetPaginatedCampaignsAsync(Guid businessId, PaginatedRequest request)
        {
            var query = _context.Campaigns
                .Where(c => c.BusinessId == businessId)
                .OrderByDescending(c => c.CreatedAt);

            var total = await query.CountAsync();

            var items = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(c => new CampaignSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Status = c.Status,
                    ScheduledAt = c.ScheduledAt,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return new PaginatedResponse<CampaignSummaryDto>
            {
                Items = items,
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        public async Task<bool> UpdateCampaignAsync(Guid id, CampaignCreateDto dto)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null || campaign.Status != "Draft")
                return false;

            // ✅ Extract BusinessId from current campaign
            var businessId = campaign.BusinessId;

            // ✅ Optional CTA ownership validation
            if (dto.CtaId.HasValue)
            {
                var cta = await _context.CTADefinitions
                    .FirstOrDefaultAsync(c => c.Id == dto.CtaId && c.BusinessId == businessId && c.IsActive);

                if (cta == null)
                    throw new UnauthorizedAccessException("❌ The selected CTA does not belong to your business or is inactive.");
            }

            // ✏️ Update campaign fields
            campaign.Name = dto.Name;
            campaign.MessageTemplate = dto.MessageTemplate;
            campaign.TemplateId = dto.TemplateId;
            campaign.FollowUpTemplateId = dto.FollowUpTemplateId;
            campaign.CampaignType = dto.CampaignType;
            campaign.CtaId = dto.CtaId;
            campaign.ImageUrl = dto.ImageUrl;
            campaign.ImageCaption = dto.ImageCaption;
            campaign.UpdatedAt = DateTime.UtcNow;
            // 🔒 Step 2.1: Refresh snapshot on update when template may have changed
            try
            {
                var effectiveTemplateName = !string.IsNullOrWhiteSpace(campaign.TemplateId)
                    ? campaign.TemplateId!
                    : (campaign.MessageTemplate ?? "");

                if (!string.IsNullOrWhiteSpace(effectiveTemplateName))
                {
                    var snapshotMeta = await _templateFetcherService.GetTemplateMetaAsync(
                        businessId,
                        effectiveTemplateName,
                        language: null,
                        provider: campaign.Provider
                    );

                    campaign.TemplateSchemaSnapshot = snapshotMeta != null
                        ? JsonConvert.SerializeObject(snapshotMeta)
                        : JsonConvert.SerializeObject(new
                        {
                            Provider = campaign.Provider ?? "",
                            TemplateName = effectiveTemplateName,
                            Language = "" // unknown if not in provider meta
                        });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Template schema snapshot (update) failed for campaign {CampaignId}", id);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CampaignDeletionResult> DeleteCampaignAsync(
        Guid businessId,
        Guid id,
        CampaignDeletionOptions options,
        CancellationToken ct = default)
        {
            // 1) Ownership + existence
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId, ct);

            if (campaign == null)
                return CampaignDeletionResult.NotFound();

            // 2) Usage snapshot (for UX & guards)
            var recipients = await _context.CampaignRecipients.CountAsync(r => r.CampaignId == id, ct);

            var activeJobStatuses = new[] { "queued", "pending", "retry", "scheduled", "inprogress", "processing", "sending" };
            var queuedJobs = await _context.OutboundCampaignJobs
                .CountAsync(j => j.CampaignId == id && activeJobStatuses.Contains((j.Status ?? "").ToLower()), ct);

            var sendLogs = await _context.CampaignSendLogs.CountAsync(s => s.CampaignId == id, ct);

            var isSending = ((campaign.Status ?? "").Equals("sending", StringComparison.OrdinalIgnoreCase)) || queuedJobs > 0;

            if (isSending && !options.Force)
                return CampaignDeletionResult.BlockedSending(recipients, queuedJobs, sendLogs);

            var clean = recipients == 0 && queuedJobs == 0 && sendLogs == 0;
            if (!options.Force && !clean)
                return CampaignDeletionResult.BlockedState(recipients, queuedJobs, sendLogs);

            // 3) Transaction: purge RESTRICT chains first, then remove campaign (CASCADE handles the rest)
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 3A) Collect dependent ids (for TrackingLogs purge)
                var msgIds = await _context.MessageLogs
                    .Where(m => m.BusinessId == businessId && m.CampaignId == id)
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                var sendIds = await _context.CampaignSendLogs
                    .Where(s => s.BusinessId == businessId && s.CampaignId == id)
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                // 3B) Purge TrackingLogs (FKs to Campaign/MessageLog/SendLog are RESTRICT)
#if NET7_0_OR_GREATER
                await _context.TrackingLogs
                    .Where(t => t.CampaignId == id
                        || (t.MessageLogId != null && msgIds.Contains(t.MessageLogId.Value))
                        || (t.CampaignSendLogId != null && sendIds.Contains(t.CampaignSendLogId.Value)))
                    .ExecuteDeleteAsync(ct);
#else
        // EF Core < 7 fallback
                    await _db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""TrackingLogs""
            WHERE ""CampaignId"" = {0}
               OR (""MessageLogId"" IS NOT NULL AND ""MessageLogId"" = ANY({1}))
               OR (""CampaignSendLogId"" IS NOT NULL AND ""CampaignSendLogId"" = ANY({2}));
            ", new object[] { id, msgIds.ToArray(), sendIds.ToArray() }, ct);
#endif

                // 3C) Clear any outstanding jobs (if not configured to cascade)
#if NET7_0_OR_GREATER
                await _context.OutboundCampaignJobs
                    .Where(j => j.CampaignId == id)
                    .ExecuteDeleteAsync(ct);
#else
        var jobs = await _db.OutboundCampaignJobs.Where(j => j.CampaignId == id).ToListAsync(ct);
        _db.OutboundCampaignJobs.RemoveRange(jobs);
        await _db.SaveChangesAsync(ct);
#endif

                // 3D) Delete the campaign (CASCADE removes audiences, members, recipients,
                //     send logs, message logs, status logs, etc. per your Fluent config)
                _context.Campaigns.Remove(campaign);
                await _context.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

                return CampaignDeletionResult.Deleted(recipients, queuedJobs, sendLogs);
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                return new CampaignDeletionResult { Status = CampaignDeletionStatus.Error };
            }
        }

        #endregion

        #region // 🆕 CreateCampaignAsync(Text/Image)

        //public async Task<Guid?> CreateTextCampaignAsync(CampaignCreateDto dto, Guid businessId, string createdBy)
        //{
        //    try
        //    {
        //        // === NEW: duplicate-name guard (per Business) ===
        //        // If you have soft-delete or archived states, add those filters here.
        //        var nameExists = await _context.Campaigns
        //            .AsNoTracking()
        //            .AnyAsync(c => c.BusinessId == businessId && c.Name == dto.Name);

        //        if (nameExists)
        //            throw new InvalidOperationException("A campaign with this name already exists for this business. Please choose a different name.");

        //        var campaignId = Guid.NewGuid();

        //        // 🔁 Parse/normalize template parameters once
        //        var parsedParams = TemplateParameterHelper.ParseTemplateParams(
        //            JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>())
        //        );

        //        // 🔒 Validate + resolve sender (optional but recommended)
        //        string? providerNorm = null;
        //        if (!string.IsNullOrWhiteSpace(dto.PhoneNumberId))
        //        {
        //            var pair = await _whisatsAppSenderService.ResolveSenderPairAsync(businessId, dto.PhoneNumberId);
        //            if (pair == null)
        //                throw new InvalidOperationException("❌ Selected sender is invalid or does not belong to this business.");
        //            providerNorm = pair.Value.Provider; // already normalized to UPPER
        //        }

        //        // 🔄 Flow id from UI (null/empty => no flow)
        //        Guid? incomingFlowId = (dto.CTAFlowConfigId.HasValue && dto.CTAFlowConfigId.Value != Guid.Empty)
        //            ? dto.CTAFlowConfigId.Value
        //            : (Guid?)null;

        //        Guid? savedFlowId = incomingFlowId;

        //        // 🧩 FLOW VALIDATION (only to align the starting template)
        //        string selectedTemplateName = dto.TemplateId ?? dto.MessageTemplate ?? string.Empty;

        //        CTAFlowConfig? flow = null;
        //        CTAFlowStep? entryStep = null;

        //        if (incomingFlowId.HasValue)
        //        {
        //            flow = await _context.CTAFlowConfigs
        //                .Include(f => f.Steps).ThenInclude(s => s.ButtonLinks)
        //                .FirstOrDefaultAsync(f =>
        //                    f.Id == incomingFlowId.Value &&
        //                    f.BusinessId == businessId &&
        //                    f.IsActive);

        //            if (flow != null)
        //            {
        //                var allIncoming = new HashSet<Guid>(flow.Steps
        //                    .SelectMany(s => s.ButtonLinks)
        //                    .Where(l => l.NextStepId.HasValue)
        //                    .Select(l => l.NextStepId!.Value));

        //                entryStep = flow.Steps
        //                    .OrderBy(s => s.StepOrder)
        //                    .FirstOrDefault(s => !allIncoming.Contains(s.Id));

        //                if (entryStep != null &&
        //                    !string.Equals(selectedTemplateName, entryStep.TemplateToSend, StringComparison.OrdinalIgnoreCase))
        //                {
        //                    selectedTemplateName = entryStep.TemplateToSend;
        //                }
        //            }
        //        }

        //        //var template = await _templateFetcherService.GetTemplateByNameAsync(
        //        //    businessId,
        //        //    selectedTemplateName,
        //        //    includeButtons: true
        //        //);

        //        //var templateBody = template?.Body ?? dto.MessageTemplate ?? string.Empty;

        //        var template = await _templateFetcherService.GetTemplateByNameAsync(
        //            businessId,
        //            selectedTemplateName,
        //            includeButtons: true
        //        );
        //        if (template == null)
        //            throw new InvalidOperationException($"Template '{selectedTemplateName}' not found from provider.");

        //        var templateBody = template.Body ?? string.Empty;


        //        var resolvedBody = TemplateParameterHelper.FillPlaceholders(templateBody, parsedParams);

        //        // =========================
        //        // 🆕 Header kind + URL logic
        //        // =========================
        //        string headerKind = (dto.HeaderKind ?? "").Trim().ToLowerInvariant(); // "image" | "video" | "document" | "text" | "none"
        //        bool isMediaHeader = headerKind == "image" || headerKind == "video" || headerKind == "document";

        //        // Prefer new unified HeaderMediaUrl; fall back to ImageUrl for legacy image campaigns
        //        string? headerUrl = string.IsNullOrWhiteSpace(dto.HeaderMediaUrl)
        //            ? (headerKind == "image" ? dto.ImageUrl : null)
        //            : dto.HeaderMediaUrl;

        //        // ✅ Campaign type: headerKind ALWAYS wins (FE may still send "text")
        //        string finalCampaignType = isMediaHeader
        //            ? headerKind                               // "image" | "video" | "document"
        //            : (dto.CampaignType ?? "text").Trim().ToLowerInvariant();

        //        // clamp to known values
        //        if (finalCampaignType != "image" &&
        //            finalCampaignType != "video" &&
        //            finalCampaignType != "document")
        //        {
        //            finalCampaignType = "text";
        //        }

        //        // Validate media header needs URL
        //        if (isMediaHeader && string.IsNullOrWhiteSpace(headerUrl))
        //            throw new InvalidOperationException("❌ Header media URL is required for this template.");

        //        // =========================================
        //        // Create entity with correct media fields set
        //        // =========================================
        //        var campaign = new Campaign
        //        {
        //            Id = campaignId,
        //            BusinessId = businessId,
        //            Name = dto.Name,

        //            MessageTemplate = dto.MessageTemplate,
        //            TemplateId = selectedTemplateName,

        //            FollowUpTemplateId = dto.FollowUpTemplateId,
        //            CampaignType = finalCampaignType,
        //            CtaId = dto.CtaId,
        //            CTAFlowConfigId = savedFlowId,

        //            ScheduledAt = dto.ScheduledAt, // FE should send UTC ISO
        //            CreatedBy = createdBy,
        //            CreatedAt = DateTime.UtcNow,
        //            UpdatedAt = DateTime.UtcNow,

        //            // Default status will be finalized below after we reason about ScheduledAt
        //            Status = "Draft",

        //            // Media fields (set exactly one depending on header kind)
        //            ImageUrl = headerKind == "image" ? headerUrl : null,
        //            ImageCaption = dto.ImageCaption,
        //            VideoUrl = headerKind == "video" ? headerUrl : null,
        //            DocumentUrl = headerKind == "document" ? headerUrl : null,

        //            TemplateParameters = JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>()),
        //            MessageBody = resolvedBody,

        //            // 🟢 Persist sender choice (nullable if not selected)
        //            Provider = providerNorm,
        //            PhoneNumberId = dto.PhoneNumberId
        //        };

        //        // 🔒 Step 2.1: Snapshot template schema (text path)
        //        try
        //        {
        //            var snapshotMeta = await _templateFetcherService.GetTemplateMetaAsync(
        //                businessId,
        //                selectedTemplateName,
        //                language: null,
        //                provider: providerNorm?.ToLowerInvariant() // normalize to match DB ("meta_cloud"/"pinnacle")
        //            );

        //            campaign.TemplateSchemaSnapshot = snapshotMeta != null
        //                ? JsonConvert.SerializeObject(snapshotMeta)
        //                : JsonConvert.SerializeObject(new
        //                {
        //                    Provider = providerNorm ?? "",
        //                    TemplateName = selectedTemplateName,
        //                    Language = template?.Language ?? ""
        //                });
        //        }
        //        catch (Exception ex)
        //        {
        //            Log.Warning(ex, "⚠️ Template schema snapshot failed for campaign {CampaignId}", campaignId);
        //        }

        //        await _context.Campaigns.AddAsync(campaign);

        //        if (dto.ContactIds != null && dto.ContactIds.Any())
        //        {
        //            var recipients = dto.ContactIds.Select(contactId => new CampaignRecipient
        //            {
        //                Id = Guid.NewGuid(),
        //                CampaignId = campaignId,
        //                ContactId = contactId,
        //                BusinessId = businessId,
        //                Status = "Pending",
        //                SentAt = null,
        //                UpdatedAt = DateTime.UtcNow
        //            });

        //            await _context.CampaignRecipients.AddRangeAsync(recipients);
        //        }

        //        if (dto.MultiButtons != null && dto.MultiButtons.Any())
        //        {
        //            var buttons = dto.MultiButtons
        //                .Where(btn => !string.IsNullOrWhiteSpace(btn.ButtonText) && !string.IsNullOrWhiteSpace(btn.TargetUrl))
        //                .Take(3)
        //                .Select((btn, index) => new CampaignButton
        //                {
        //                    Id = Guid.NewGuid(),
        //                    CampaignId = campaignId,
        //                    Title = btn.ButtonText,
        //                    Type = btn.ButtonType ?? "url",
        //                    Value = btn.TargetUrl,
        //                    Position = index + 1,
        //                    IsFromTemplate = false
        //                });

        //            await _context.CampaignButtons.AddRangeAsync(buttons);
        //        }

        //        if (template != null && template.ButtonParams?.Count > 0)
        //        {
        //            var buttonsToSave = new List<CampaignButton>();
        //            var userButtons = dto.ButtonParams ?? new List<CampaignButtonParamFromMetaDto>();

        //            var total = Math.Min(3, template.ButtonParams.Count);
        //            for (int i = 0; i < total; i++)
        //            {
        //                var tplBtn = template.ButtonParams[i];
        //                var isDynamic = (tplBtn.ParameterValue?.Contains("{{1}}") ?? false);

        //                var userBtn = userButtons.FirstOrDefault(b => b.Position == i + 1);
        //                var valueToSave = (isDynamic && userBtn != null)
        //                    ? userBtn.Value?.Trim()
        //                    : tplBtn.ParameterValue;

        //                buttonsToSave.Add(new CampaignButton
        //                {
        //                    Id = Guid.NewGuid(),
        //                    CampaignId = campaignId,
        //                    Title = tplBtn.Text,
        //                    Type = tplBtn.Type,
        //                    Value = valueToSave,
        //                    Position = i + 1,
        //                    IsFromTemplate = true
        //                });
        //            }

        //            await _context.CampaignButtons.AddRangeAsync(buttonsToSave);
        //        }

        //        // === NEW: schedule-aware status + enqueue job ===
        //        // If ScheduledAt is in the future → mark as "Queued"/"Scheduled" and enqueue a job due at ScheduledAt.
        //        // Else keep as "Draft" (user can send manually).
        //        var nowUtc = DateTime.UtcNow;
        //        if (campaign.ScheduledAt.HasValue && campaign.ScheduledAt.Value > nowUtc)
        //        {
        //            campaign.Status = "Queued"; // name it "Scheduled" if you have that enumeration in UI
        //            campaign.UpdatedAt = nowUtc;

        //            // One campaign → one active queued job. Since this is a fresh campaign, no job yet.
        //            var job = new OutboundCampaignJob
        //            {
        //                Id = Guid.NewGuid(),
        //                BusinessId = businessId,
        //                CampaignId = campaign.Id,
        //                Status = "queued",
        //                Attempt = 0,
        //                MaxAttempts = 5,
        //                // CRITICAL: use UTC moment from ScheduledAt
        //                NextAttemptAt = campaign.ScheduledAt.Value
        //            };
        //            await _context.OutboundCampaignJobs.AddAsync(job);
        //        }
        //        else
        //        {
        //            // Draft by default; user can trigger "Send now" elsewhere
        //            campaign.Status = "Draft";
        //        }

        //        await _context.SaveChangesAsync();

        //        Log.Information("✅ Campaign '{Name}' created | Type:{Type} | Header:{HeaderKind} | FlowId:{Flow} | EntryTemplate:{Entry} | Sender:{Provider}/{PhoneId} | Recipients:{Contacts} | ScheduledAt:{ScheduledAt} | Status:{Status}",
        //            dto.Name, finalCampaignType, headerKind,
        //            savedFlowId,
        //            entryStep?.TemplateToSend ?? selectedTemplateName,
        //            providerNorm,
        //            dto.PhoneNumberId,
        //            dto.ContactIds?.Count ?? 0,
        //            campaign.ScheduledAt,
        //            campaign.Status);

        //        return campaignId;
        //    }
        //    catch (Exception ex)
        //    {
        //        // If you want the FE to show the exact message (like duplicate-name),
        //        // make sure your controller surfaces ex.Message in the response body.
        //        Log.Error(ex, "❌ Failed to create campaign");
        //        return null;
        //    }
        //}
        public async Task<Guid?> CreateTextCampaignAsync(CampaignCreateDto dto, Guid businessId, string createdBy)
        {
            try
            {
                // === duplicate-name guard (per Business) ===
                var nameExists = await _context.Campaigns
                    .AsNoTracking()
                    .AnyAsync(c => c.BusinessId == businessId && c.Name == dto.Name);
                if (nameExists)
                    throw new InvalidOperationException("A campaign with this name already exists for this business. Please choose a different name.");

                var campaignId = Guid.NewGuid();

                // Parse template parameters once
                var parsedParams = TemplateParameterHelper.ParseTemplateParams(
                    JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>())
                );

                // Validate + resolve sender (optional)
                string? providerNorm = null;
                if (!string.IsNullOrWhiteSpace(dto.PhoneNumberId))
                {
                    var pair = await _whisatsAppSenderService.ResolveSenderPairAsync(businessId, dto.PhoneNumberId);
                    if (pair == null)
                        throw new InvalidOperationException("❌ Selected sender is invalid or does not belong to this business.");
                    providerNorm = pair.Value.Provider; // normalized to UPPER
                }

                // Flow id from UI (null/empty => no flow)
                Guid? incomingFlowId = (dto.CTAFlowConfigId.HasValue && dto.CTAFlowConfigId.Value != Guid.Empty)
                    ? dto.CTAFlowConfigId.Value
                    : (Guid?)null;

                Guid? savedFlowId = incomingFlowId;

                // FLOW VALIDATION (align starting template if flow provided)
                string selectedTemplateName = dto.TemplateId ?? dto.MessageTemplate ?? string.Empty;

                CTAFlowConfig? flow = null;
                CTAFlowStep? entryStep = null;

                if (incomingFlowId.HasValue)
                {
                    flow = await _context.CTAFlowConfigs
                        .Include(f => f.Steps).ThenInclude(s => s.ButtonLinks)
                        .FirstOrDefaultAsync(f =>
                            f.Id == incomingFlowId.Value &&
                            f.BusinessId == businessId &&
                            f.IsActive);

                    if (flow != null)
                    {
                        var allIncoming = new HashSet<Guid>(flow.Steps
                            .SelectMany(s => s.ButtonLinks)
                            .Where(l => l.NextStepId.HasValue)
                            .Select(l => l.NextStepId!.Value));

                        entryStep = flow.Steps
                            .OrderBy(s => s.StepOrder)
                            .FirstOrDefault(s => !allIncoming.Contains(s.Id));

                        if (entryStep != null &&
                            !string.Equals(selectedTemplateName, entryStep.TemplateToSend, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedTemplateName = entryStep.TemplateToSend;
                        }
                    }
                }

                // === Fetch template strictly from DB (includes header kind + combined body if TEXT header) ===
                var template = await _templateFetcherService.GetTemplateByNameAsync(
                    businessId,
                    selectedTemplateName,
                    includeButtons: true
                );
                if (template == null)
                    throw new InvalidOperationException($"Template '{selectedTemplateName}' not found.");

                // Use template's combined body (TEXT header + body) so placeholders in header are also filled
                var templateBody = template.Body ?? string.Empty;
                var resolvedBody = TemplateParameterHelper.FillPlaceholders(templateBody, parsedParams);

                // =========================
                // Header kind + URL logic
                // =========================
                // Trust the template’s header kind; fall back to FE only if template says "none"
                var headerKind = (template.HeaderKind ?? dto.HeaderKind ?? "none").Trim().ToLowerInvariant();

                // If FE sent a conflicting kind, fail early (helps catch wrong selections)
                if (!string.IsNullOrWhiteSpace(dto.HeaderKind) &&
                    !string.Equals(headerKind, dto.HeaderKind.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Selected template requires a '{headerKind}' header, but request specified '{dto.HeaderKind}'.");
                }

                bool isMediaHeader = headerKind is "image" or "video" or "document";

                // Prefer unified HeaderMediaUrl; legacy ImageUrl still supported for "image"
                string? headerUrl = string.IsNullOrWhiteSpace(dto.HeaderMediaUrl)
                    ? (headerKind == "image" ? dto.ImageUrl : null)
                    : dto.HeaderMediaUrl;

                // For media headers, URL is mandatory
                if (isMediaHeader && string.IsNullOrWhiteSpace(headerUrl))
                    throw new InvalidOperationException("❌ Header media URL is required for this template.");

                // Campaign type: headerKind drives media campaigns; otherwise use FE value (default text)
                string finalCampaignType = isMediaHeader
                    ? headerKind                                   // "image" | "video" | "document"
                    : (dto.CampaignType ?? "text").Trim().ToLowerInvariant();

                if (finalCampaignType is not ("image" or "video" or "document"))
                    finalCampaignType = "text";

                // =========================================
                // Create entity with correct media fields set
                // =========================================
                var campaign = new Campaign
                {
                    Id = campaignId,
                    BusinessId = businessId,
                    Name = dto.Name,

                    MessageTemplate = dto.MessageTemplate,
                    TemplateId = selectedTemplateName,

                    FollowUpTemplateId = dto.FollowUpTemplateId,
                    CampaignType = finalCampaignType,
                    CtaId = dto.CtaId,
                    CTAFlowConfigId = savedFlowId,

                    ScheduledAt = dto.ScheduledAt, // FE should send UTC ISO
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,

                    Status = "Draft",

                    // Media fields (set exactly one depending on header kind)
                    ImageUrl = headerKind == "image" ? headerUrl : null,
                    ImageCaption = dto.ImageCaption,
                    VideoUrl = headerKind == "video" ? headerUrl : null,
                    DocumentUrl = headerKind == "document" ? headerUrl : null,

                    TemplateParameters = JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>()),
                    MessageBody = resolvedBody,

                    Provider = providerNorm,
                    PhoneNumberId = dto.PhoneNumberId
                };

                // Snapshot template schema (from DB meta)
                try
                {
                    var snapshotMeta = await _templateFetcherService.GetTemplateMetaAsync(
                        businessId,
                        selectedTemplateName,
                        language: null,
                        provider: providerNorm?.ToLowerInvariant()
                    );

                    campaign.TemplateSchemaSnapshot = snapshotMeta != null
                        ? JsonConvert.SerializeObject(snapshotMeta)
                        : JsonConvert.SerializeObject(new
                        {
                            Provider = providerNorm ?? "",
                            TemplateName = selectedTemplateName,
                            Language = template.Language ?? ""
                        });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠️ Template schema snapshot failed for campaign {CampaignId}", campaignId);
                }

                await _context.Campaigns.AddAsync(campaign);

                // Recipients
                if (dto.ContactIds != null && dto.ContactIds.Any())
                {
                    var recipients = dto.ContactIds.Select(contactId => new CampaignRecipient
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaignId,
                        ContactId = contactId,
                        BusinessId = businessId,
                        Status = "Pending",
                        SentAt = null,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await _context.CampaignRecipients.AddRangeAsync(recipients);
                }

                // Custom buttons from FE
                if (dto.MultiButtons != null && dto.MultiButtons.Any())
                {
                    var buttons = dto.MultiButtons
                        .Where(btn => !string.IsNullOrWhiteSpace(btn.ButtonText) && !string.IsNullOrWhiteSpace(btn.TargetUrl))
                        .Take(3)
                        .Select((btn, index) => new CampaignButton
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaignId,
                            Title = btn.ButtonText,
                            Type = btn.ButtonType ?? "url",
                            Value = btn.TargetUrl,
                            Position = index + 1,
                            IsFromTemplate = false
                        });

                    await _context.CampaignButtons.AddRangeAsync(buttons);
                }

                // Buttons coming from the template (DB metadata)
                if (template.ButtonParams?.Count > 0)
                {
                    var buttonsToSave = new List<CampaignButton>();
                    var userButtons = dto.ButtonParams ?? new List<CampaignButtonParamFromMetaDto>();

                    var total = Math.Min(3, template.ButtonParams.Count);
                    for (int i = 0; i < total; i++)
                    {
                        var tplBtn = template.ButtonParams[i];
                        var isDynamic = (tplBtn.ParameterValue?.Contains("{{1}}") ?? false);

                        var userBtn = userButtons.FirstOrDefault(b => b.Position == i + 1);
                        var valueToSave = (isDynamic && userBtn != null)
                            ? userBtn.Value?.Trim()
                            : tplBtn.ParameterValue;

                        buttonsToSave.Add(new CampaignButton
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaignId,
                            Title = tplBtn.Text,
                            Type = tplBtn.Type,
                            Value = valueToSave,
                            Position = i + 1,
                            IsFromTemplate = true
                        });
                    }

                    await _context.CampaignButtons.AddRangeAsync(buttonsToSave);
                }

                // schedule-aware status + enqueue job
                var nowUtc = DateTime.UtcNow;
                if (campaign.ScheduledAt.HasValue && campaign.ScheduledAt.Value > nowUtc)
                {
                    campaign.Status = "Queued";
                    campaign.UpdatedAt = nowUtc;

                    var job = new OutboundCampaignJob
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        CampaignId = campaign.Id,
                        Status = "queued",
                        Attempt = 0,
                        MaxAttempts = 5,
                        NextAttemptAt = campaign.ScheduledAt.Value
                    };
                    await _context.OutboundCampaignJobs.AddAsync(job);
                }
                else
                {
                    campaign.Status = "Draft";
                }

                await _context.SaveChangesAsync();

                Log.Information("✅ Campaign '{Name}' created | Type:{Type} | Header:{HeaderKind} | FlowId:{Flow} | EntryTemplate:{Entry} | Sender:{Provider}/{PhoneId} | Recipients:{Contacts} | ScheduledAt:{ScheduledAt} | Status:{Status}",
                    dto.Name, finalCampaignType, headerKind,
                    savedFlowId,
                    entryStep?.TemplateToSend ?? selectedTemplateName,
                    providerNorm,
                    dto.PhoneNumberId,
                    dto.ContactIds?.Count ?? 0,
                    campaign.ScheduledAt,
                    campaign.Status);

                return campaignId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to create campaign");
                return null;
            }
        }

        public async Task<Guid> CreateImageCampaignAsync(Guid businessId, CampaignCreateDto dto, string createdBy)
        {
            var campaignId = Guid.NewGuid();

            // 🔁 Parse/normalize template parameters once
            var parsedParams = TemplateParameterHelper.ParseTemplateParams(
                JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>())
            );

            // 🔄 Flow id from UI (null/empty => no flow). We will persist this as-is.
            Guid? incomingFlowId = (dto.CTAFlowConfigId.HasValue && dto.CTAFlowConfigId.Value != Guid.Empty)
                ? dto.CTAFlowConfigId.Value
                : (Guid?)null;

            // We will save this value regardless of validation outcome
            Guid? savedFlowId = incomingFlowId;

            // ============================================================
            // 🧩 FLOW VALIDATION (only to align the starting template)
            // ============================================================
            string selectedTemplateName = dto.TemplateId ?? dto.MessageTemplate ?? string.Empty;

            CTAFlowConfig? flow = null;
            CTAFlowStep? entryStep = null;

            if (incomingFlowId.HasValue)
            {
                // load flow with steps+links and verify ownership
                flow = await _context.CTAFlowConfigs
                    .Include(f => f.Steps).ThenInclude(s => s.ButtonLinks)
                    .FirstOrDefaultAsync(f =>
                        f.Id == incomingFlowId.Value &&
                        f.BusinessId == businessId &&
                        f.IsActive);

                if (flow == null)
                {
                    Log.Warning("❌ Flow {FlowId} not found/active for business {Biz}. Will persist FlowId but not align template.",
                        incomingFlowId, businessId);
                }
                else
                {
                    // compute entry step: step with NO incoming links
                    var allIncoming = new HashSet<Guid>(flow.Steps
                        .SelectMany(s => s.ButtonLinks)
                        .Where(l => l.NextStepId.HasValue)
                        .Select(l => l.NextStepId!.Value));

                    entryStep = flow.Steps
                        .OrderBy(s => s.StepOrder)
                        .FirstOrDefault(s => !allIncoming.Contains(s.Id));

                    if (entryStep == null)
                    {
                        Log.Warning("❌ Flow {FlowId} has no entry step. Persisting FlowId but not aligning template.", flow.Id);
                    }
                    else if (!string.Equals(selectedTemplateName, entryStep.TemplateToSend, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Information("ℹ️ Aligning selected template '{Sel}' to flow entry '{Entry}'.",
                            selectedTemplateName, entryStep.TemplateToSend);
                        selectedTemplateName = entryStep.TemplateToSend;
                    }
                }
            }
            else
            {
                Log.Information("ℹ️ No flow attached to image campaign '{Name}'. Proceeding as plain template campaign.", dto.Name);
            }

            // 🧠 Fetch template (for body + buttons) using the aligned/selected template name
            var template = await _templateFetcherService.GetTemplateByNameAsync(
                businessId,
                selectedTemplateName,
                includeButtons: true
            );

            // 🧠 Resolve message body using template body (if available) else dto.MessageTemplate
            var templateBody = template?.Body ?? dto.MessageTemplate ?? string.Empty;
            var resolvedBody = TemplateParameterHelper.FillPlaceholders(templateBody, parsedParams);

            // 🎯 Step 1: Create campaign (CTAFlowConfigId now always = savedFlowId)
            var campaign = new Campaign
            {
                Id = campaignId,
                BusinessId = businessId,
                Name = dto.Name,

                // store the (possibly aligned) template name
                MessageTemplate = dto.MessageTemplate,      // keep original text for UI if you use it
                TemplateId = selectedTemplateName,          // ensure start template matches flow entry when available

                FollowUpTemplateId = dto.FollowUpTemplateId,
                CampaignType = "image",
                CtaId = dto.CtaId,
                CTAFlowConfigId = savedFlowId,              // 👈 persist what UI sent (or null if no flow)

                ScheduledAt = dto.ScheduledAt,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = "Draft",

                // image-specific fields
                ImageUrl = dto.ImageUrl,
                ImageCaption = dto.ImageCaption,

                // params/body snapshot (useful for previews & auditing)
                TemplateParameters = JsonConvert.SerializeObject(dto.TemplateParameters ?? new List<string>()),
                MessageBody = resolvedBody
            };
            // 🔒 Step 2.1: Snapshot template schema (image path)
            try
            {
                var snapshotMeta = await _templateFetcherService.GetTemplateMetaAsync(
                    businessId,
                    selectedTemplateName,
                    language: null,
                    provider: null
                );

                campaign.TemplateSchemaSnapshot = snapshotMeta != null
                    ? JsonConvert.SerializeObject(snapshotMeta)
                    : JsonConvert.SerializeObject(new
                    {
                        Provider = "",
                        TemplateName = selectedTemplateName,
                        Language = template?.Language ?? ""
                    });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Template schema snapshot failed for (image) campaign {CampaignId}", campaignId);
            }

            await _context.Campaigns.AddAsync(campaign);

            // ✅ Step 2: Assign contacts (leave SentAt null until send)
            if (dto.ContactIds != null && dto.ContactIds.Any())
            {
                var recipients = dto.ContactIds.Select(contactId => new CampaignRecipient
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId,
                    ContactId = contactId,
                    BusinessId = businessId,
                    Status = "Pending",
                    SentAt = null,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.CampaignRecipients.AddRangeAsync(recipients);
            }

            // ✅ Step 3a: Save manual buttons from frontend
            if (dto.MultiButtons != null && dto.MultiButtons.Any())
            {
                var buttons = dto.MultiButtons
                    .Where(btn => !string.IsNullOrWhiteSpace(btn.ButtonText) && !string.IsNullOrWhiteSpace(btn.TargetUrl))
                    .Take(3)
                    .Select((btn, index) => new CampaignButton
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaignId,
                        Title = btn.ButtonText,
                        Type = btn.ButtonType ?? "url",
                        Value = btn.TargetUrl,
                        Position = index + 1,
                        IsFromTemplate = false
                    });

                await _context.CampaignButtons.AddRangeAsync(buttons);
            }

            // ======================== Dynamic buttons merge ========================
            // EXACTLY mirrors your text-creator pattern to avoid type issues with ButtonMetadataDto
            if (template != null && template.ButtonParams?.Count > 0)
            {
                var buttonsToSave = new List<CampaignButton>();
                var userButtons = dto.ButtonParams ?? new List<CampaignButtonParamFromMetaDto>();

                var total = Math.Min(3, template.ButtonParams.Count);
                for (int i = 0; i < total; i++)
                {
                    var tplBtn = template.ButtonParams[i];                         // ButtonMetadataDto: Text, Type, SubType, Index, ParameterValue
                    var isDynamic = (tplBtn.ParameterValue?.Contains("{{1}}") ?? false);

                    var userBtn = userButtons.FirstOrDefault(b => b.Position == i + 1);
                    var valueToSave = (isDynamic && userBtn != null)
                        ? userBtn.Value?.Trim()                                    // user override for dynamic URL
                        : tplBtn.ParameterValue;                                   // pattern or static value from meta

                    buttonsToSave.Add(new CampaignButton
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaignId,
                        Title = tplBtn.Text,                                       // from ButtonMetadataDto
                        Type = tplBtn.Type,                                        // from ButtonMetadataDto
                        Value = valueToSave,
                        Position = i + 1,
                        IsFromTemplate = true
                    });
                }

                await _context.CampaignButtons.AddRangeAsync(buttonsToSave);
            }
            // ======================================================================

            await _context.SaveChangesAsync();

            Log.Information("✅ Image campaign '{Name}' created | FlowId: {Flow} | EntryTemplate: {Entry} | Recipients: {Contacts} | UserButtons: {ManualButtons} | TemplateButtons: {TemplateButtons} | Params: {Params}",
                dto.Name,
                savedFlowId,
                entryStep?.TemplateToSend ?? selectedTemplateName,
                dto.ContactIds?.Count ?? 0,
                dto.MultiButtons?.Count ?? 0,
                template?.ButtonParams?.Count ?? 0,
                dto.TemplateParameters?.Count ?? 0
            );

            return campaignId;
        }
        #endregion

     
        public async Task<bool> SendCampaignAsync(Guid campaignId, string ipAddress, string userAgent)
        {
            // 1) Load campaign (no tracking)
            var campaign = await _context.Campaigns
                .Where(c => c.Id == campaignId)
                .Select(c => new { c.Id, c.BusinessId, c.TemplateId, MessageTemplate = c.MessageTemplate })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (campaign == null)
            {
                Log.Warning("🚫 Campaign {CampaignId} not found", campaignId);
                return false;
            }

            // 1.1) Resolve active WA settings → Provider + sender (optional)
            var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(campaign.BusinessId);
            //if (wa is null)
            //return ResponseResult.ErrorInfo("❌ WhatsApp settings not found for this business.");

            //.AsNoTracking()
            //.Where(w => w.BusinessId == campaign.BusinessId && w.IsActive)
            //.FirstOrDefaultAsync();

            var provider = wa.Provider;
            var phoneNumberId = wa.PhoneNumberId;         // optional

            // 2) Load recipients with explicit LEFT JOINs to Contact and AudienceMember
            var recipients = await (
                from r in _context.CampaignRecipients.AsNoTracking()
                where r.CampaignId == campaignId

                join c in _context.Contacts.AsNoTracking()
                    on r.ContactId equals c.Id into cg
                from c in cg.DefaultIfEmpty()

                join am in _context.AudienceMembers.AsNoTracking()
                    on r.AudienceMemberId equals am.Id into amg
                from am in amg.DefaultIfEmpty()

                select new
                {
                    r.Id,
                    r.ContactId,
                    Phone = c != null && c.PhoneNumber != null ? c.PhoneNumber : am!.PhoneE164,
                    Name = c != null && c.Name != null ? c.Name : am!.Name,
                    ParamsJson = r.ResolvedParametersJson
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Phone))
                .ToListAsync();

            if (recipients.Count == 0)
            {
                Log.Warning("🚫 Campaign {CampaignId} has no recipients", campaignId);
                return false;
            }

            // 3) Mark Sending
            var campaignRow = await _context.Campaigns.FirstAsync(c => c.Id == campaign.Id);
            campaignRow.Status = "Sending";
            campaignRow.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // 4) Parallel send
            var throttleLimit = 5;
            var total = recipients.Count;
            var sent = 0;
            var failed = 0;

            await Parallel.ForEachAsync(
                recipients,
                new ParallelOptions { MaxDegreeOfParallelism = throttleLimit },
                async (r, ct) =>
                {
                    try
                    {
                        var phone = r.Phone!;
                        // NOTE: we intentionally do NOT inject profile name here.
                        // Parameters come from frozen ResolvedParametersJson (if any).
                        var parameters = ParseParams(r.ParamsJson);

                        using var scope = _serviceProvider.CreateScope();
                        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var dto = new SimpleTemplateMessageDto
                        {
                            Provider = provider,                 // ✅ REQUIRED by send method
                            PhoneNumberId = phoneNumberId,       // optional sender override
                            RecipientNumber = phone,
                            TemplateName = campaign.TemplateId ?? campaign.MessageTemplate,
                            TemplateParameters = parameters      // ✅ use frozen params (or empty list)
                        };

                        var result = await _messageEngineService
                            .SendTemplateMessageSimpleAsync(campaign.BusinessId, dto);

                        var sendLog = new CampaignSendLog
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = campaign.BusinessId,
                            CampaignId = campaign.Id,
                            ContactId = r.ContactId,            // Guid? OK to be null
                            RecipientId = r.Id,
                            TemplateId = campaign.TemplateId,
                            MessageBody = campaign.MessageTemplate,
                            MessageId = result.MessageId,       // ✅ capture WAMID
                            SendStatus = result.Success ? "Sent" : "Failed",
                            ErrorMessage = result.Message,
                            SentAt = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            SourceChannel = "whatsapp",
                            IpAddress = ipAddress,
                            DeviceInfo = userAgent
                            // (Optional) ButtonBundleJson = SnapshotTemplateButtons(...);
                        };

                        await scopedDb.CampaignSendLogs.AddAsync(sendLog, ct);

                        var rec = await scopedDb.CampaignRecipients.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
                        if (rec != null)
                        {
                            rec.Status = result.Success ? "Sent" : "Failed";
                            rec.MessagePreview = campaign.MessageTemplate;
                            rec.SentAt = DateTime.UtcNow;
                            rec.UpdatedAt = DateTime.UtcNow;
                        }

                        await scopedDb.SaveChangesAsync(ct);

                        if (result.Success) Interlocked.Increment(ref sent);
                        else Interlocked.Increment(ref failed);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        Log.Error(ex, "❌ Send failed for recipient: {RecipientId}", r.Id);
                    }
                });

            // 5) Finalize
            campaignRow = await _context.Campaigns.FirstAsync(c => c.Id == campaignId);
            campaignRow.Status = "Sent";
            campaignRow.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Log.Information("📤 Campaign {CampaignId} sent via template to {Count} recipients (✅ {Sent}, ❌ {Failed})",
                campaignId, total, sent, failed);

            return sent > 0;

            // ---- local helpers ----
            static List<string> ParseParams(string? json)
            {
                if (string.IsNullOrWhiteSpace(json)) return new List<string>();
                try
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    return arr ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }
        public async Task<bool> SendCampaignInParallelAsync(Guid campaignId, string ipAddress, string userAgent)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Recipients)
                .ThenInclude(r => r.Contact)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null || campaign.Recipients.Count == 0)
            {
                Log.Warning("🚫 Campaign not found or has no recipients");
                return false;
            }

            campaign.Status = "Sending";
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            int maxParallelism = 5;

#if NET6_0_OR_GREATER
            await Parallel.ForEachAsync(campaign.Recipients, new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism
            },
            async (recipient, cancellationToken) =>
            {
                await SendToRecipientAsync(campaign, recipient, ipAddress, userAgent);
            });
#else
    var tasks = campaign.Recipients.Select(recipient =>
        SendToRecipientAsync(campaign, recipient, ipAddress, userAgent)
    );
    await Task.WhenAll(tasks);
#endif

            campaign.Status = "Sent";
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Log.Information("📤 Campaign {CampaignId} sent in parallel to {Count} recipients", campaign.Id, campaign.Recipients.Count);
            return true;
        }
        private async Task SendToRecipientAsync(Campaign campaign, CampaignRecipient recipient, string ip, string ua)
        {
            try
            {
                var dto = new SimpleTemplateMessageDto
                {
                    RecipientNumber = recipient.Contact.PhoneNumber,
                    TemplateName = campaign.TemplateId,// campaign.MessageTemplate,
                    TemplateParameters = new List<string> { recipient.Contact.Name ?? "Customer" }
                };

                var result = await _messageEngineService.SendTemplateMessageSimpleAsync(campaign.BusinessId, dto);


                var log = new CampaignSendLog
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    ContactId = recipient.ContactId,
                    RecipientId = recipient.Id,
                    TemplateId = campaign.TemplateId,
                    MessageBody = campaign.MessageTemplate,
                    MessageId = null,
                    SendStatus = result.Success ? "Sent" : "Failed",
                    ErrorMessage = result.Message,
                    SentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    SourceChannel = "whatsapp",
                    IpAddress = ip,
                    DeviceInfo = ua
                };

                lock (_context)
                {
                    _context.CampaignSendLogs.Add(log);
                    recipient.Status = result.Success ? "Sent" : "Failed";
                    recipient.MessagePreview = campaign.MessageTemplate;
                    recipient.SentAt = DateTime.UtcNow;
                    recipient.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to send template to recipient: {RecipientId}", recipient.Id);
            }
        }

        public async Task<bool> RemoveRecipientAsync(Guid businessId, Guid campaignId, Guid contactId)
        {
            var entry = await _context.CampaignRecipients
                .FirstOrDefaultAsync(r =>
                    r.CampaignId == campaignId &&
                    r.ContactId == contactId &&
                    r.Campaign.BusinessId == businessId); // ✅ Filter by related Campaign.BusinessId

            if (entry == null)
                return false;

            _context.CampaignRecipients.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AssignContactsToCampaignAsync(Guid campaignId, Guid businessId, List<Guid> contactIds)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Recipients)
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

            if (campaign == null)
                return false;

            var newRecipients = contactIds.Select(id => new CampaignRecipient
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                ContactId = id,
                BusinessId = businessId,
                Status = "Pending",
                SentAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _context.CampaignRecipients.AddRange(newRecipients);
            await _context.SaveChangesAsync();
            return true;
        }

        // This is the Entry point to send Temaplte (Text Based and Image Based)
        public async Task<ResponseResult> SendTemplateCampaignAsync(Guid campaignId)
        {
            try
            {
                var campaign = await _context.Campaigns
                    .Include(c => c.Recipients)
                        .ThenInclude(r => r.Contact) // 🧠 include contact details
                    .Include(c => c.MultiButtons)
                    .FirstOrDefaultAsync(c => c.Id == campaignId && !c.IsDeleted);

                if (campaign == null)
                    return ResponseResult.ErrorInfo("❌ Campaign not found.");

                if (campaign.Recipients == null || !campaign.Recipients.Any())
                    return ResponseResult.ErrorInfo("❌ No recipients assigned to this campaign.");

                var templateName = campaign.MessageTemplate;
                var templateId = campaign.TemplateId;
                var language = "en_US"; // Optional: make dynamic later
                var isImageTemplate = !string.IsNullOrEmpty(campaign.ImageUrl);

                var templateParams = JsonConvert.DeserializeObject<List<string>>(campaign.TemplateParameters ?? "[]");

                int success = 0, failed = 0;

                foreach (var recipient in campaign.Recipients)
                {
                    var messageDto = new ImageTemplateMessageDto
                    {
                        // BusinessId = campaign.BusinessId,
                        RecipientNumber = recipient.Contact.PhoneNumber,
                        TemplateName = templateName,
                        LanguageCode = language,
                        HeaderImageUrl = isImageTemplate ? campaign.ImageUrl : null,
                        TemplateParameters = templateParams,
                        ButtonParameters = campaign.MultiButtons
                            .OrderBy(b => b.Position)
                            .Take(3)
                            .Select(btn => new CampaignButtonDto
                            {
                                ButtonText = btn.Title,
                                ButtonType = btn.Type,
                                TargetUrl = btn.Value
                            }).ToList()
                    };

                    // ✅ Call the image/template sender
                    var sendResult = await _messageEngineService.SendImageTemplateMessageAsync(messageDto, campaign.BusinessId);
                    var isSuccess = sendResult.ToString().ToLower().Contains("messages");

                    var log = new MessageLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = campaign.BusinessId,
                        RecipientNumber = recipient.Contact.PhoneNumber,
                        MessageContent = campaign.MessageBody,// templateName,
                        MediaUrl = campaign.ImageUrl,
                        Status = isSuccess ? "Sent" : "Failed",
                        ErrorMessage = isSuccess ? null : "API Failure",
                        RawResponse = JsonConvert.SerializeObject(sendResult),
                        CreatedAt = DateTime.UtcNow,
                        SentAt = DateTime.UtcNow
                    };

                    await _context.MessageLogs.AddAsync(log);

                    if (isSuccess) success++;
                    else failed++;
                }

                await _context.SaveChangesAsync();
                return ResponseResult.SuccessInfo($"✅ Sent: {success}, ❌ Failed: {failed}");
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Unexpected error during campaign send.", ex.ToString());
            }
        }

        // #region  This region include all the code related to sending text and image based

        public async Task<ResponseResult> SendTemplateCampaignWithTypeDetectionAsync(Guid campaignId, CancellationToken ct = default)
        {
            var correlationId = Guid.NewGuid();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["corr"] = correlationId,
                ["campaignId"] = campaignId
            });

            _logger.LogInformation("[SendDetect] BEGIN send for campaign {CampaignId}", campaignId);

            // 1) Load a slim campaign snapshot 
            var campaignSnap = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && !c.IsDeleted)
                .Select(c => new
                {
                    c.Id,
                    c.BusinessId,
                    c.CampaignType,
                    c.TemplateId,
                    c.MessageTemplate,
                    c.TemplateParameters,
                    c.ImageUrl,
                    c.VideoUrl,
                    c.DocumentUrl,
                    c.CTAFlowConfigId,
                    Recipients = c.Recipients
                        .Where(r =>
                            (r.Contact != null && r.Contact.PhoneNumber != null && r.Contact.PhoneNumber != "") ||
                            (r.AudienceMember != null && (
                                (r.AudienceMember.PhoneE164 != null && r.AudienceMember.PhoneE164 != "") ||
                                (r.AudienceMember.PhoneRaw != null && r.AudienceMember.PhoneRaw != "")
                            )))
                        .Select(r => new
                        {
                            r.Id,
                            r.ContactId,
                            ContactPhone = r.Contact != null ? r.Contact.PhoneNumber : null,
                            ContactName = r.Contact != null ? r.Contact.Name : null,
                            AMPhoneE164 = r.AudienceMember != null ? r.AudienceMember.PhoneE164 : null,
                            AMPhoneRaw = r.AudienceMember != null ? r.AudienceMember.PhoneRaw : null,
                            AMName = r.AudienceMember != null ? r.AudienceMember.Name : null,
                            AMAttributes = r.AudienceMember != null ? r.AudienceMember.AttributesJson : null
                        })
                        .ToList(),
                    Buttons = c.MultiButtons
                        .Select(b => new { b.Id, b.Position, b.Title, b.Type, b.Value })
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (campaignSnap == null)
            {
                _logger.LogWarning("[SendDetect] ❌ Campaign not found");
                return ResponseResult.ErrorInfo("❌ Campaign not found.");
            }

            _logger.LogInformation("[SendDetect] Loaded campaign snapshot: biz={Biz} recipients={Recipients} tplId={TplId} msgTpl={MsgTpl}",
                campaignSnap.BusinessId, campaignSnap.Recipients.Count, campaignSnap.TemplateId, campaignSnap.MessageTemplate);

            if (campaignSnap.Recipients.Count == 0)
            {
                _logger.LogWarning("[SendDetect] ⚠️ No valid recipients with phone. total=0");
                return ResponseResult.ErrorInfo("⚠️ No valid recipients with phone numbers (checked Contact.PhoneNumber and AudienceMember.PhoneE164/PhoneRaw).");
            }

            // 2) Flow entry template
            var (_, entryTemplate) = await ResolveFlowEntryAsync(campaignSnap.BusinessId, campaignSnap.CTAFlowConfigId);
            var tplName =
                !string.IsNullOrWhiteSpace(entryTemplate) ? entryTemplate! :
                !string.IsNullOrWhiteSpace(campaignSnap.TemplateId) ? campaignSnap.TemplateId! :
                !string.IsNullOrWhiteSpace(campaignSnap.MessageTemplate) ? campaignSnap.MessageTemplate! :
                string.Empty;

            if (string.IsNullOrWhiteSpace(tplName))
            {
                _logger.LogWarning("[SendDetect] ❌ No template name (TemplateId/MessageTemplate/FlowEntry empty).");
                return ResponseResult.ErrorInfo("❌ Campaign has no template name (TemplateId/MessageTemplate is empty).");
            }

            _logger.LogInformation("[SendDetect] Using template '{TplName}' (flowEntry={FlowEntry}, TemplateId={TplId}, MessageTemplate={MsgTpl})",
                tplName, entryTemplate, campaignSnap.TemplateId, campaignSnap.MessageTemplate);

            // 3) Provider from settings
            var setting = await _context.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == campaignSnap.BusinessId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (setting == null)
            {
                _logger.LogWarning("[SendDetect] ❌ WhatsApp settings not found for biz={Biz}", campaignSnap.BusinessId);
                return ResponseResult.ErrorInfo("❌ WhatsApp settings not found for this business.");
            }

            var providerFromDb = (setting.Provider ?? string.Empty).Trim();
            var providerUpper = providerFromDb.ToUpperInvariant();
            _logger.LogInformation("[SendDetect] Provider={Provider}", providerFromDb);

            // 4) Template row from DB
            var templateRow = await _context.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == campaignSnap.BusinessId
                         && t.IsActive
                         && t.Name == tplName
                         && t.Provider == providerUpper)
                .OrderByDescending(t => t.UpdatedAt)
                .ThenByDescending(t => t.LastSyncedAt)
                .FirstOrDefaultAsync(ct);

            if (templateRow == null)
            {
                _logger.LogWarning("[SendDetect] ❌ Template not found in DB. biz={Biz} provider={Provider} name={Name}",
                    campaignSnap.BusinessId, providerFromDb, tplName);
                return ResponseResult.ErrorInfo("❌ Campaign template not found in DB for this provider.");
            }

            _logger.LogInformation("[SendDetect] Template row ok: name={Name} lang={Lang} status={Status} bodyLen={Len} placeholderCount(DB)={PC}",
                templateRow.Name, templateRow.LanguageCode, templateRow.Status, templateRow.Body?.Length ?? 0, templateRow.BodyVarCount);

            // Recompute BODY placeholder count (supports POSITIONAL vs NAMED)
            int recomputedBodyCount = 0;
            try
            {
                recomputedBodyCount = CountBodyPlaceholdersFromRaw(
                    templateRow.RawJson,
                    templateRow.Body,
                    templateRow.ParameterFormat // <- now passed through
                );
                if (recomputedBodyCount != Math.Max(0, templateRow.BodyVarCount))
                {
                    _logger.LogWarning("[SendDetect] PlaceholderCount mismatch: DB={DbCount} recomputed(BODY)={Recomputed} tpl={Tpl}",
                        templateRow.BodyVarCount, recomputedBodyCount, templateRow.Name);
                }
                else
                {
                    _logger.LogDebug("[SendDetect] PlaceholderCount verified: {Count}", recomputedBodyCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendDetect] Failed to recompute BODY placeholder count for tpl={Tpl}", templateRow.Name);
            }

            // 5) Infer media type
            var headerKind = TemplateHeaderInspector.Infer(templateRow);
            var inferredType = TemplateHeaderInspector.ToMediaType(headerKind);

            var mediaType = (campaignSnap.CampaignType ?? string.Empty).Trim();
            mediaType = string.IsNullOrEmpty(mediaType) || mediaType.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? inferredType
                : mediaType.ToLowerInvariant();

            _logger.LogInformation("[SendDetect] MediaType resolved: campaignType={CampaignType} inferred={Inferred} => chosen={Chosen}",
                campaignSnap.CampaignType, inferredType, mediaType);

            // 6) Validate media URLs
            if (mediaType == "image" && string.IsNullOrWhiteSpace(campaignSnap.ImageUrl))
                return ResponseResult.ErrorInfo("🚫 Image template requires ImageUrl on the campaign.");
            if (mediaType == "video" && string.IsNullOrWhiteSpace(campaignSnap.VideoUrl))
                return ResponseResult.ErrorInfo("🚫 Video template requires VideoUrl on the campaign.");
            if (mediaType == "document" && string.IsNullOrWhiteSpace(campaignSnap.DocumentUrl))
                return ResponseResult.ErrorInfo("🚫 Document template requires DocumentUrl on the campaign.");

            // 6.1 Parameter mapping support
            var parameterFormat = templateRow.ParameterFormat; // "POSITIONAL" | "NAMED"
            var bodyParamNames = ExtractBodyParamNamesFromTemplate(templateRow.RawJson, templateRow.Body, parameterFormat);

            // 7) Rehydrate light Campaign for downstream
            var campaign = new Campaign
            {
                Id = campaignSnap.Id,
                BusinessId = campaignSnap.BusinessId,
                CampaignType = mediaType,
                TemplateId = tplName,
                MessageTemplate = tplName,
                TemplateParameters = campaignSnap.TemplateParameters,
                ImageUrl = campaignSnap.ImageUrl,
                VideoUrl = campaignSnap.VideoUrl,
                DocumentUrl = campaignSnap.DocumentUrl,
                CTAFlowConfigId = campaignSnap.CTAFlowConfigId,
                Provider = providerFromDb,
                MultiButtons = campaignSnap.Buttons
                    .OrderBy(b => b.Position)
                    .Select(b => new CampaignButton
                    {
                        Id = b.Id,
                        Position = b.Position,
                        Title = b.Title,
                        Type = b.Type,
                        Value = b.Value
                    })
                    .ToList(),
                Recipients = campaignSnap.Recipients.Select(r => new CampaignRecipient
                {
                    Id = r.Id,
                    ContactId = r.ContactId,
                    Contact = r.ContactPhone != null ? new Contact
                    {
                        PhoneNumber = r.ContactPhone,
                        Name = r.ContactName
                    } : null,
                    AudienceMember = (r.AMPhoneE164 != null || r.AMPhoneRaw != null) ? new AudienceMember
                    {
                        PhoneE164 = r.AMPhoneE164,
                        PhoneRaw = r.AMPhoneRaw,
                        Name = r.AMName,
                        AttributesJson = r.AMAttributes   // <-- add this
                    } : null
                }).ToList()
            };

            // 8) Unified enqueue (text | image | video | document)
            {
                var enqueueSw = System.Diagnostics.Stopwatch.StartNew();

                var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(campaign.BusinessId);
                if (wa is null)
                    return ResponseResult.ErrorInfo("❌ WhatsApp settings not found.");

                var languageCode = string.IsNullOrWhiteSpace(templateRow.LanguageCode) ? "en_US" : templateRow.LanguageCode!.Trim();

                var buttonsOrdered = campaign.MultiButtons?
                    .Select((b, idx) => new { Btn = b, idx })
                    .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                    .ThenBy(x => x.idx)
                    .Select(x => x.Btn)
                    .ToList() ?? new List<CampaignButton>();

                string? phoneNumberId = !string.IsNullOrWhiteSpace(campaign.PhoneNumberId)
                    ? campaign.PhoneNumberId
                    : wa.PhoneNumberId;

                var isMeta = string.Equals(providerFromDb, "META_CLOUD", StringComparison.OrdinalIgnoreCase);
                if (isMeta && string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo("❌ No PhoneNumberId configured for Meta Cloud sender.");

                static string? ResolvePhone(CampaignRecipient r) =>
                    r?.Contact?.PhoneNumber ?? r?.AudienceMember?.PhoneE164 ?? r?.AudienceMember?.PhoneRaw;

                var placeholderCount = Math.Max(0, recomputedBodyCount);

                var templateMeta = new TemplateMetadataDto
                {
                    Name = templateRow.Name,
                    Language = languageCode,
                    Body = templateRow.Body,
                    PlaceholderCount = placeholderCount,
                    ButtonParams = string.IsNullOrWhiteSpace(templateRow.UrlButtons)
                        ? new List<ButtonMetadataDto>()
                        : (JsonConvert.DeserializeObject<List<ButtonMetadataDto>>(templateRow.UrlButtons!)
                            ?? new List<ButtonMetadataDto>())
                };

                _logger.LogInformation("[SendDetect] Buttons: templateHas={TplBtnCount} campaignHas={CampBtnCount}",
                    templateMeta.ButtonParams.Count, buttonsOrdered.Count);

                var now = DateTime.UtcNow;
                var items = new List<(CampaignRecipient r, string paramsJson, string btnsJson, string? headerUrl, string idemKey)>();

                int recipientsWithPhone = 0, recipientsMissingParams = 0;
                foreach (var r in campaign.Recipients)
                {
                    var phone = ResolvePhone(r);
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        _logger.LogWarning("[SendDetect] Skip recipient {RecipientId}: empty phone", r.Id);
                        continue;
                    }
                    recipientsWithPhone++;

                    // BODY params (now supports POSITIONAL and NAMED)
                    var resolvedParams = GetRecipientBodyParams(
                        r,
                        placeholderCount,
                        campaign.TemplateParameters,
                        bodyParamNames,
                        parameterFormat
                    );

                    // Missing parameter check — use FILLED count (not just list length)
                    var filled = resolvedParams.Count(v => !string.IsNullOrWhiteSpace(v));
                    if (placeholderCount > 0 && filled < placeholderCount)
                    {
                        recipientsMissingParams++;
                        var why = $"Missing body parameter(s): expected {placeholderCount}, got {filled}.";
                        _logger.LogWarning("[SendDetect] Skip recipient {RecipientId}: {Why} resolved=[{Vals}] phone={Phone}",
                            r.Id, why, string.Join("|", resolvedParams), phone);

                        var logIdLocal = Guid.NewGuid();
                        _context.MessageLogs.Add(new MessageLog
                        {
                            Id = logIdLocal,
                            BusinessId = campaign.BusinessId,
                            CampaignId = campaign.Id,
                            ContactId = r.ContactId,
                            RecipientNumber = phone,
                            MessageContent = campaign.TemplateId,
                            Status = "Failed",
                            ErrorMessage = why,
                            RawResponse = "{\"local_error\":\"missing_template_body_params\"}",
                            CreatedAt = now,
                            Source = "campaign",
                            CTAFlowConfigId = campaign.CTAFlowConfigId
                        });

                        await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaign.Id,
                            BusinessId = campaign.BusinessId,
                            ContactId = r.ContactId,
                            RecipientId = r.Id,
                            MessageBody = campaign.MessageBody ?? campaign.TemplateId,
                            TemplateId = campaign.TemplateId!,
                            SendStatus = "Failed",
                            MessageLogId = logIdLocal,
                            ErrorMessage = why,
                            CreatedAt = now,
                            CTAFlowConfigId = campaign.CTAFlowConfigId
                        }, ct);

                        continue;
                    }

                    // Resolve dynamic URL buttons for snapshot (using your existing builders)
                    var contactForTemplating = r.Contact ?? new Contact
                    {
                        Id = Guid.Empty,
                        BusinessId = campaign.BusinessId,
                        PhoneNumber = phone,
                        Name = r.Contact?.Name ?? r.AudienceMember?.Name ?? "Customer"
                    };

                    var dummySendLogId = Guid.NewGuid();
                    List<string> resolvedButtonUrls;

                    var btnComponents = string.Equals(providerFromDb, "PINNACLE", StringComparison.OrdinalIgnoreCase)
                        ? BuildTextTemplateComponents_Pinnacle(resolvedParams, buttonsOrdered, templateMeta, dummySendLogId, contactForTemplating, out resolvedButtonUrls)
                        : BuildTextTemplateComponents_Meta(resolvedParams, buttonsOrdered, templateMeta, dummySendLogId, contactForTemplating, out resolvedButtonUrls);

                    _logger.LogDebug("[SendDetect] Recipient {RecipientId}: bodyResolved=[{Vals}] btnResolved=[{BtnVals}] btnCompCount={BtnCompCount}",
                        r.Id, string.Join("|", resolvedParams), string.Join(" , ", resolvedButtonUrls ?? new List<string>()), (btnComponents?.Count ?? 0));

                    // persist materialization snapshot
                    var recStub = new CampaignRecipient { Id = r.Id };
                    _context.CampaignRecipients.Attach(recStub);
                    recStub.ResolvedParametersJson = JsonConvert.SerializeObject(resolvedParams);
                    recStub.ResolvedButtonUrlsJson = JsonConvert.SerializeObject(resolvedButtonUrls);
                    recStub.MaterializedAt = now;
                    recStub.UpdatedAt = now;

                    string? headerUrl = mediaType switch
                    {
                        "image" => campaign.ImageUrl,
                        "video" => campaign.VideoUrl,
                        "document" => campaign.DocumentUrl,
                        _ => null
                    };

                    var paramsJson = JsonConvert.SerializeObject(resolvedParams);
                    var btnsJson = JsonConvert.SerializeObject(resolvedButtonUrls);
                    var idemKey = Idempotency.Sha256(
                        $"{campaign.Id}|{phone}|{campaign.TemplateId}|{paramsJson}|{btnsJson}|{mediaType}|{headerUrl}");

                    items.Add((r, paramsJson, btnsJson, headerUrl, idemKey));

                    _logger.LogInformation("[SendDetect] +EnqueueCandidate recipient={RecipientId} phone={Phone} idemKeyHashPrefix={Hash}",
                        r.Id, phone, idemKey.Substring(0, 8));
                }

                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("[SendDetect] Recipients summary: withPhone={WithPhone} missingParams={Missing} candidates={Candidates}",
                    recipientsWithPhone, recipientsMissingParams, items.Count);

                if (items.Count == 0)
                    return ResponseResult.ErrorInfo("No messages enqueued. Missing data for all recipients.");

                try
                {
                    _logger.LogInformation("[SendDetect] EnqueueOutboundJobsAsync start: mediaType={Media} template={Tpl} lang={Lang} phoneNumberId={PhoneId}",
                        mediaType, campaign.TemplateId, languageCode, phoneNumberId);

                    await EnqueueOutboundJobsAsync(
                        campaign: campaign,
                        provider: providerFromDb,
                        mediaType: mediaType,
                        templateName: campaign.TemplateId!,
                        languageCode: languageCode,
                        phoneNumberId: phoneNumberId,
                        items: items
                    );

                    enqueueSw.Stop();
                    _logger.LogInformation("[SendDetect] Enqueue done in {Ms} ms. Items={Count}", enqueueSw.ElapsedMilliseconds, items.Count);

                    sw.Stop();
                    _logger.LogInformation("[SendDetect] ✅ SUCCESS in {Ms} ms", sw.ElapsedMilliseconds);

                    return ResponseResult.SuccessInfo($"📤 Enqueued {items.Count} {mediaType} message(s) for async delivery.");
                }
                catch (Exception ex)
                {
                    enqueueSw.Stop();
                    _logger.LogError(ex, "[SendDetect] ❌ Enqueue failed after {Ms} ms", enqueueSw.ElapsedMilliseconds);
                    return ResponseResult.ErrorInfo("❌ Failed to enqueue outbound jobs.");
                }
            }

            // ── local: BODY placeholder counter (POSITIONAL vs NAMED) ───────────────────────
            int CountBodyPlaceholdersFromRaw(string? raw, string? bodyFallback, string? parameterFormat = null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(raw);
                        if (doc.RootElement.TryGetProperty("components", out var comps) &&
                            comps.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var c in comps.EnumerateArray())
                            {
                                if (c.TryGetProperty("type", out var typeProp) &&
                                    string.Equals(typeProp.GetString(), "BODY", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (c.TryGetProperty("text", out var textProp))
                                    {
                                        var body = textProp.GetString();
                                        return CountCurlies(body, parameterFormat);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // ignore parse errors
                }
                catch { /* diagnostics only */ }

                return CountCurlies(bodyFallback, parameterFormat);

                static int CountCurlies(string? s, string? parameterFormat)
                {
                    if (string.IsNullOrEmpty(s)) return 0;

                    var fmt = (parameterFormat ?? "POSITIONAL").Trim().ToUpperInvariant();

                    if (fmt == "POSITIONAL")
                    {
                        // Only {{1}}, {{2}}, ...
                        return System.Text.RegularExpressions.Regex.Matches(s, @"\{\{\s*\d+\s*\}\}").Count;
                    }
                    else if (fmt == "NAMED")
                    {
                        // Only {{name_like_this}}
                        return System.Text.RegularExpressions.Regex.Matches(s, @"\{\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\}\}").Count;
                    }

                    return 0;
                }
            }
        }


        private static List<string> GetRecipientBodyParams(
            CampaignRecipient r,
            int expectedCount,
            string? campaignTemplateParameters,
            List<string>? bodyParamNames,
            string? parameterFormat)
        {
            // 1) Start from campaign-level values (often "[]", i.e., empty)
            var values = TemplateParameterHelper.ParseTemplateParams(campaignTemplateParameters)?.ToList()
                         ?? new List<string>();

            // 2) Trim/pad to expected slots
            if (values.Count > expectedCount) values = values.Take(expectedCount).ToList();
            while (values.Count < expectedCount) values.Add(string.Empty);

            // 3) Fill gaps from per-recipient attributes
            var fmt = (parameterFormat ?? "POSITIONAL").Trim().ToUpperInvariant();
            var attrs = ParseAttributes(r); // e.g., { parameter1: "Nicolus Benz", parameter2: "Email" }

            if (attrs != null)
            {
                if (fmt == "POSITIONAL")
                {
                    for (int i = 0; i < expectedCount; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(values[i])) continue;

                        var key = "parameter" + (i + 1);      // {{1}} ← parameter1, {{2}} ← parameter2 ...
                        if (attrs.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                            values[i] = v!;
                    }
                }
                else if (fmt == "NAMED" && bodyParamNames != null && bodyParamNames.Count > 0)
                {
                    // Map named placeholders from attributes with matching keys
                    var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < bodyParamNames.Count; i++)
                        if (!nameToIndex.ContainsKey(bodyParamNames[i])) nameToIndex[bodyParamNames[i]] = i;

                    foreach (var kv in attrs)
                    {
                        if (nameToIndex.TryGetValue(kv.Key, out var idx) &&
                            idx < values.Count &&
                            string.IsNullOrWhiteSpace(values[idx]) &&
                            !string.IsNullOrWhiteSpace(kv.Value))
                        {
                            values[idx] = kv.Value!;
                        }
                    }
                }
            }

            // 4) Friendly default for slot 1 if still empty
            if (expectedCount > 0 && values.Count > 0 && string.IsNullOrWhiteSpace(values[0]))
                values[0] = r?.Contact?.Name ?? r?.AudienceMember?.Name ?? string.Empty;

            // 5) Normalize whitespace
            for (int i = 0; i < values.Count; i++)
                values[i] = values[i]?.Trim() ?? string.Empty;

            return values;
        }
        private static Dictionary<string, string>? ParseAttributes(CampaignRecipient r)
        {
            var raw = r?.AudienceMember?.AttributesJson;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
                if (obj == null) return null;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in obj)
                    dict[kv.Key] = kv.Value?.ToString() ?? string.Empty;

                return dict;
            }
            catch
            {
                return null;
            }
        }

        private static List<string>? ExtractBodyParamNamesFromTemplate(string? rawJson, string? bodyFallback, string? parameterFormat)
        {
            var fmt = (parameterFormat ?? "POSITIONAL").Trim().ToUpperInvariant();
            if (fmt != "NAMED") return null;

            // 1) Try provider RAW JSON → components[type=BODY].text
            string? bodyText = null;
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
                    if (doc.RootElement.TryGetProperty("components", out var comps) &&
                        comps.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var c in comps.EnumerateArray())
                        {
                            if (c.TryGetProperty("type", out var tp) &&
                                string.Equals(tp.GetString(), "BODY", StringComparison.OrdinalIgnoreCase) &&
                                c.TryGetProperty("text", out var tprop))
                            {
                                bodyText = tprop.GetString();
                                break;
                            }
                        }
                    }
                }
                catch { /* ignore malformed provider JSON */ }
            }

            bodyText ??= bodyFallback;
            if (string.IsNullOrWhiteSpace(bodyText)) return new List<string>(0);

            // 2) Pull {{name_like_this}} in order; keep unique, valid identifiers
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(bodyText, @"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}"))
            {
                var name = m.Groups[1].Value;
                if (seen.Add(name)) names.Add(name);
            }
            return names;
        }

        public async Task<ResponseResult> SendTextTemplateCampaignAsync(Campaign campaign)
        {
            try
            {
                if (campaign == null || campaign.IsDeleted)
                    return ResponseResult.ErrorInfo("❌ Invalid campaign.");
                if (campaign.Recipients == null || campaign.Recipients.Count == 0)
                    return ResponseResult.ErrorInfo("❌ No recipients to send.");

                var businessId = campaign.BusinessId;

                // 0) Filter recipients that actually have a phone
                static string? ResolveRecipientPhone(CampaignRecipient r) =>
                    r?.Contact?.PhoneNumber ?? r?.AudienceMember?.PhoneE164 ?? r?.AudienceMember?.PhoneRaw;

                var recipients = campaign.Recipients
                    .Where(r => !string.IsNullOrWhiteSpace(ResolveRecipientPhone(r)))
                    .ToList();

                if (recipients.Count == 0)
                    return ResponseResult.ErrorInfo("⚠️ No valid recipients with phone numbers (Contact/AudienceMember).");

                // 1) Flow/template selection
                var (_, entryTemplate) = await ResolveFlowEntryAsync(businessId, campaign.CTAFlowConfigId);
                var templateKey = !string.IsNullOrWhiteSpace(entryTemplate)
                    ? entryTemplate!.Trim()
                    : (campaign.TemplateId ?? campaign.MessageTemplate ?? "").Trim();

                if (string.IsNullOrWhiteSpace(templateKey))
                    return ResponseResult.ErrorInfo("❌ No template selected.");

                // 2) WhatsApp settings
                var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(businessId);
                if (wa is null)
                    return ResponseResult.ErrorInfo("❌ WhatsApp settings not found.");

                // DB stores Provider in UPPERCASE
                var provider = (wa.Provider ?? string.Empty).Trim();
                var providerUpper = provider.ToUpperInvariant();

                // 3) Fetch template from DB (NO network)
                // Try provider TemplateId first; if not found, fall back to Name.
                WhatsAppTemplate? templateRow = await _context.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId &&
                                t.IsActive &&
                                t.Provider == providerUpper &&
                                t.TemplateId == templateKey)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.LastSyncedAt)
                    .FirstOrDefaultAsync();

                if (templateRow is null)
                {
                    templateRow = await _context.WhatsAppTemplates
                        .AsNoTracking()
                        .Where(t => t.BusinessId == businessId &&
                                    t.IsActive &&
                                    t.Provider == providerUpper &&
                                    t.Name == templateKey)
                        .OrderByDescending(t => t.UpdatedAt)
                        .ThenByDescending(t => t.LastSyncedAt)
                        .FirstOrDefaultAsync();
                }

                if (templateRow == null)
                    return ResponseResult.ErrorInfo("❌ Template not found in DB for this business/provider.");

                // Map DB row -> DTO used by builders
                var templateMeta = new TemplateMetadataDto
                {
                    Name = templateRow.Name,
                    Language = templateRow.LanguageCode,
                    Body = templateRow.Body,
                    // FINAL MODEL: body-only placeholders live in BodyVarCount
                    PlaceholderCount = Math.Max(0, templateRow.BodyVarCount),
                    ButtonParams = ParseUrlButtonsToButtonMeta(templateRow.UrlButtons)
                };

                var languageCode = (templateMeta.Language ?? "").Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                    return ResponseResult.ErrorInfo("❌ Template language missing in DB.");

                // 4) Campaign buttons (order by Position)
                var buttons = campaign.MultiButtons?.OrderBy(b => b.Position).ToList()
                              ?? new List<CampaignButton>();

                // Sender override; if missing, use WA DTO value
                string? phoneNumberIdOverride = !string.IsNullOrWhiteSpace(campaign.PhoneNumberId)
                    ? campaign.PhoneNumberId
                    : wa.PhoneNumberId;

                // Meta Cloud must have a PhoneNumberId
                if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberIdOverride))
                    return ResponseResult.ErrorInfo("❌ No PhoneNumberId configured for Meta Cloud sender.");

                // 5) Optional flow entry step id
                Guid? entryStepId = null;
                if (campaign.CTAFlowConfigId.HasValue)
                {
                    entryStepId = await _context.CTAFlowSteps
                        .Where(s => s.CTAFlowConfigId == campaign.CTAFlowConfigId.Value)
                        .OrderBy(s => s.StepOrder)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync();
                }

                // 6) Freeze button bundle for click-tracking (from template meta)
                string? buttonBundleJson = null;
                if (templateMeta.ButtonParams is { Count: > 0 })
                {
                    var bundle = templateMeta.ButtonParams
                        .OrderBy(b => b.Index)
                        .Take(3)
                        .Select((b, i) => new
                        {
                            i,
                            position = i + 1,
                            text = (b.Text ?? "").Trim(),
                            type = b.Type,
                            subType = b.SubType
                        })
                        .ToList();
                    buttonBundleJson = JsonConvert.SerializeObject(bundle);
                }

                // 7) Preload AudienceMember phone/name for recipients that don’t have a Contact
                var neededMemberIds = recipients
                    .Where(x => x.ContactId == null && x.AudienceMemberId != null)
                    .Select(x => x.AudienceMemberId!.Value)
                    .Distinct()
                    .ToList();

                var audienceLookup = neededMemberIds.Count == 0
                    ? new Dictionary<Guid, (string Phone, string? Name)>()
                    : await _context.AudienceMembers.AsNoTracking()
                        .Where(m => m.BusinessId == businessId && neededMemberIds.Contains(m.Id))
                        .Select(m => new { m.Id, m.PhoneE164, m.PhoneRaw, m.Name })
                        .ToDictionaryAsync(
                            x => x.Id,
                            x => (Phone: string.IsNullOrWhiteSpace(x.PhoneE164) ? (x.PhoneRaw ?? "") : x.PhoneE164,
                                  Name: x.Name)
                        );

                int successCount = 0, failureCount = 0;
                var now = DateTime.UtcNow;
                // Derive format + named BODY param names from the stored template
                var parameterFormat = (templateRow.ParameterFormat ?? "POSITIONAL").Trim().ToUpperInvariant();
                var bodyParamNames = ExtractBodyParamNamesFromTemplate(templateRow.RawJson, templateRow.Body, parameterFormat);

                foreach (var r in recipients)
                {
                    // Resolve actual phone + fallback name
                    var phone = ResolveRecipientPhone(r);
                    string? name = r.Contact?.Name;

                    if (string.IsNullOrWhiteSpace(phone) && r.AudienceMemberId != null &&
                        audienceLookup.TryGetValue(r.AudienceMemberId.Value, out var a) &&
                        !string.IsNullOrWhiteSpace(a.Phone))
                    {
                        phone = a.Phone;
                        name ??= a.Name ?? "Customer";
                    }

                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        failureCount++;
                        continue; // nothing to send to
                    }

                    // For templating only (do NOT attach to EF)
                    var contactForTemplating = r.Contact ?? new Contact
                    {
                        Id = Guid.Empty,
                        BusinessId = businessId,
                        PhoneNumber = phone,
                        Name = name ?? "Customer"
                    };

                    var runId = Guid.NewGuid();
                    var campaignSendLogId = Guid.NewGuid();

                    // BODY params per recipient
                    // var resolvedParams = GetRecipientBodyParams(r, templateMeta.PlaceholderCount, campaign.TemplateParameters);
                    var resolvedParams = GetRecipientBodyParams(
                        r,
                        templateMeta.PlaceholderCount,
                        campaign.TemplateParameters,
                        bodyParamNames,
                        parameterFormat
                    );
                    // If template expects body placeholders, block send when blanks exist
                    if (templateMeta.PlaceholderCount > 0 && resolvedParams.Any(string.IsNullOrWhiteSpace))
                    {
                        failureCount++;
                        var why = $"Missing body parameter(s): expected {templateMeta.PlaceholderCount}, got {resolvedParams.Count(x => !string.IsNullOrWhiteSpace(x))} filled.";

                        // Attach recipient stub to update without creating related entities
                        var recStub1 = new CampaignRecipient { Id = r.Id };
                        _context.CampaignRecipients.Attach(recStub1);
                        recStub1.MaterializedAt = now;
                        recStub1.UpdatedAt = now;
                        recStub1.ResolvedParametersJson = JsonConvert.SerializeObject(resolvedParams);

                        // Log locally as a failed send without calling provider
                        var logIdLocal = Guid.NewGuid();
                        _context.MessageLogs.Add(new MessageLog
                        {
                            Id = logIdLocal,
                            BusinessId = businessId,
                            CampaignId = campaign.Id,
                            ContactId = r.ContactId, // may be null
                            RecipientNumber = phone,
                            MessageContent = campaign.MessageBody,
                            Status = "Failed",
                            ErrorMessage = why,
                            RawResponse = "{\"local_error\":\"missing_template_body_params\"}",
                            CreatedAt = now,
                            Source = "campaign",
                            CTAFlowConfigId = campaign.CTAFlowConfigId,
                            CTAFlowStepId = entryStepId,
                            ButtonBundleJson = buttonBundleJson,
                            RunId = runId
                        });

                        await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                        {
                            Id = campaignSendLogId,
                            CampaignId = campaign.Id,
                            BusinessId = businessId,
                            ContactId = r.ContactId,  // may be null
                            RecipientId = r.Id,
                            MessageBody = campaign.MessageBody ?? templateKey,
                            TemplateId = templateKey,
                            SendStatus = "Failed",
                            MessageLogId = logIdLocal,
                            ErrorMessage = why,
                            CreatedAt = now,
                            CreatedBy = campaign.CreatedBy,
                            CTAFlowConfigId = campaign.CTAFlowConfigId,
                            CTAFlowStepId = entryStepId,
                            ButtonBundleJson = buttonBundleJson,
                            RunId = runId
                        });

                        continue; // skip provider call
                    }

                    // Build components using DB template meta
                    List<string> resolvedButtonUrls;
                    object components = providerUpper == "PINNACLE"
                        ? BuildTextTemplateComponents_Pinnacle(resolvedParams, buttons, templateMeta, campaignSendLogId, contactForTemplating, out resolvedButtonUrls)
                        : BuildTextTemplateComponents_Meta(resolvedParams, buttons, templateMeta, campaignSendLogId, contactForTemplating, out resolvedButtonUrls);

                    var payload = new
                    {
                        messaging_product = "whatsapp",
                        to = phone,
                        type = "template",
                        template = new
                        {
                            name = templateKey,
                            language = new { code = languageCode }, // from DB
                            components
                        }
                    };

                    // Freeze recipient materialization BEFORE send — attach via STUB
                    var recStub2 = new CampaignRecipient { Id = r.Id };
                    _context.CampaignRecipients.Attach(recStub2);
                    recStub2.ResolvedParametersJson = JsonConvert.SerializeObject(resolvedParams);
                    recStub2.ResolvedButtonUrlsJson = JsonConvert.SerializeObject(resolvedButtonUrls);
                    recStub2.MaterializedAt = now;
                    recStub2.UpdatedAt = now;
                    recStub2.IdempotencyKey = Idempotency.Sha256($"{campaign.Id}|{phone}|{templateKey}|{recStub2.ResolvedParametersJson}|{recStub2.ResolvedButtonUrlsJson}");

                    var result = await _messageEngineService.SendPayloadAsync(businessId, providerUpper, payload, phoneNumberIdOverride);

                    var logId = Guid.NewGuid();
                    _context.MessageLogs.Add(new MessageLog
                    {
                        Id = logId,
                        BusinessId = businessId,
                        CampaignId = campaign.Id,
                        ContactId = r.ContactId, // may be null
                        RecipientNumber = phone,
                        MessageContent = templateKey,
                        Status = result.Success ? "Sent" : "Failed",
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        RawResponse = result.RawResponse,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        Source = "campaign",
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId
                    });

                    await _billingIngest.IngestFromSendResponseAsync(
                        businessId: businessId,
                        messageLogId: logId,
                        provider: providerUpper,
                        rawResponseJson: result.RawResponse ?? "{}"
                    );

                    await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                    {
                        Id = campaignSendLogId,
                        CampaignId = campaign.Id,
                        BusinessId = businessId,
                        ContactId = r.ContactId,  // may be null
                        RecipientId = r.Id,
                        MessageBody = campaign.MessageBody ?? templateKey,
                        TemplateId = templateKey,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        MessageLogId = logId,
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        CreatedBy = campaign.CreatedBy,
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId
                    });

                    if (result.Success) successCount++; else failureCount++;
                }

                await _context.SaveChangesAsync();
                return ResponseResult.SuccessInfo($"📤 Sent to {successCount} recipients. ❌ Failed for {failureCount}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while sending text template campaign");
                return ResponseResult.ErrorInfo("🚨 Unexpected error while sending campaign.", ex.ToString());
            }
        }

        private static List<ButtonMetadataDto> ParseUrlButtonsToButtonMeta(string? urlButtonsJson)
        {
            if (string.IsNullOrWhiteSpace(urlButtonsJson)) return new List<ButtonMetadataDto>();

            try
            {
                var arr = Newtonsoft.Json.Linq.JArray.Parse(urlButtonsJson);
                var list = new List<ButtonMetadataDto>(arr.Count);

                foreach (var j in arr)
                {
                    var idx = (int?)(j?["index"]) ?? list.Count;
                    list.Add(new ButtonMetadataDto
                    {
                        Index = idx,
                        Type = "URL",
                        SubType = "url",
                        Text = "",          // label isn’t stored in UrlButtons; keep empty
                        ParameterValue = null        // may be resolved later; parameters exist in j["parameters"]
                    });
                }
                return list;
            }
            catch
            {
                return new List<ButtonMetadataDto>();
            }
        }

        private static bool IsHttpsMp4Url(string? url, out string? normalizedError)
        {
            normalizedError = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                normalizedError = "Missing VideoUrl.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                normalizedError = "VideoUrl is not a valid absolute URL.";
                return false;
            }

            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                normalizedError = "VideoUrl must use HTTPS.";
                return false;
            }

            var ext = Path.GetExtension(uri.AbsolutePath);
            if (!ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                normalizedError = "VideoUrl must point to an .mp4 file.";
                return false;
            }

            return true;
        }
        public async Task<ResponseResult> SendVideoTemplateCampaignAsync(Campaign campaign)
        {
            try
            {
                if (campaign == null || campaign.IsDeleted)
                    return ResponseResult.ErrorInfo("❌ Invalid campaign.");
                if (campaign.Recipients == null || campaign.Recipients.Count == 0)
                    return ResponseResult.ErrorInfo("❌ No recipients to send.");

                var businessId = campaign.BusinessId;

                static string? PhoneOf(CampaignRecipient r) =>
                    r?.Contact?.PhoneNumber ?? r?.AudienceMember?.PhoneE164 ?? r?.AudienceMember?.PhoneRaw;

                var recipients = campaign.Recipients.Where(r => !string.IsNullOrWhiteSpace(PhoneOf(r))).ToList();
                if (recipients.Count == 0)
                    return ResponseResult.ErrorInfo("⚠️ No valid recipients with phone numbers.");

                // Flow/template selection
                var (_, entryTemplate) = await ResolveFlowEntryAsync(businessId, campaign.CTAFlowConfigId);
                var templateKey = !string.IsNullOrWhiteSpace(entryTemplate)
                    ? entryTemplate!.Trim()
                    : (campaign.TemplateId ?? campaign.MessageTemplate ?? "").Trim();
                if (string.IsNullOrWhiteSpace(templateKey))
                    return ResponseResult.ErrorInfo("❌ No template selected.");

                // Validate header media URL (must be https .mp4)
                var videoUrl = (campaign.VideoUrl ?? campaign.ImageUrl ?? "").Trim();
                if (!IsHttpsMp4Url(videoUrl, out var vErr))
                    return ResponseResult.ErrorInfo("🚫 Invalid VideoUrl", vErr);

                // 1) WhatsApp settings
                var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(businessId);
                if (wa is null)
                    return ResponseResult.ErrorInfo("❌ WhatsApp settings not found.");

                // DB stores Provider UPPERCASE
                var providerUpper = (wa.Provider ?? string.Empty).Trim().ToUpperInvariant();

                // 2) Fetch template from DB (NO network) — try TemplateId first, then Name
                WhatsAppTemplate? templateRow = await _context.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId &&
                                t.IsActive &&
                                t.Provider == providerUpper &&
                                t.TemplateId == templateKey)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.LastSyncedAt)
                    .FirstOrDefaultAsync();

                if (templateRow is null)
                {
                    templateRow = await _context.WhatsAppTemplates
                        .AsNoTracking()
                        .Where(t => t.BusinessId == businessId &&
                                    t.IsActive &&
                                    t.Provider == providerUpper &&
                                    t.Name == templateKey)
                        .OrderByDescending(t => t.UpdatedAt)
                        .ThenByDescending(t => t.LastSyncedAt)
                        .FirstOrDefaultAsync();
                }

                if (templateRow == null)
                    return ResponseResult.ErrorInfo("❌ Template metadata not found in DB.");

                var templateMeta = new TemplateMetadataDto
                {
                    Name = templateRow.Name,
                    Language = templateRow.LanguageCode,
                    Body = templateRow.Body,
                    // FINAL MODEL: BODY-only placeholder count
                    PlaceholderCount = Math.Max(0, templateRow.BodyVarCount),
                    // If you need URL-button structure, adapt UrlButtons here:
                    ButtonParams = ParseUrlButtonsToButtonMeta(templateRow.UrlButtons)
                };

                var languageCode = (templateMeta.Language ?? "").Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                    return ResponseResult.ErrorInfo("❌ Template language missing in DB.");

                // 3) Sender id
                string? phoneNumberIdOverride = !string.IsNullOrWhiteSpace(campaign.PhoneNumberId)
                    ? campaign.PhoneNumberId
                    : wa.PhoneNumberId;

                if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberIdOverride))
                    return ResponseResult.ErrorInfo("❌ No PhoneNumberId configured for Meta Cloud sender.");

                // 4) Optional flow entry step id
                Guid? entryStepId = null;
                if (campaign.CTAFlowConfigId.HasValue)
                {
                    entryStepId = await _context.CTAFlowSteps
                        .Where(s => s.CTAFlowConfigId == campaign.CTAFlowConfigId.Value)
                        .OrderBy(s => s.StepOrder)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync();
                }

                // 5) Freeze button bundle for UI click tracking
                string? buttonBundleJson = null;
                if (templateMeta.ButtonParams is { Count: > 0 })
                {
                    var bundle = templateMeta.ButtonParams
                        .OrderBy(b => b.Index)
                        .Take(3)
                        .Select((b, i) => new { i, position = i + 1, text = (b.Text ?? "").Trim(), type = b.Type, subType = b.SubType })
                        .ToList();
                    buttonBundleJson = JsonConvert.SerializeObject(bundle);
                }

                // 6) Audience lookup for missing contacts
                var neededMemberIds = recipients
                    .Where(x => x.ContactId == null && x.AudienceMemberId != null)
                    .Select(x => x.AudienceMemberId!.Value)
                    .Distinct()
                    .ToList();

                var audienceLookup = neededMemberIds.Count == 0
                    ? new Dictionary<Guid, (string Phone, string? Name)>()
                    : await _context.AudienceMembers.AsNoTracking()
                        .Where(m => m.BusinessId == businessId && neededMemberIds.Contains(m.Id))
                        .Select(m => new { m.Id, m.PhoneE164, m.PhoneRaw, m.Name })
                        .ToDictionaryAsync(
                            x => x.Id,
                            x => (Phone: string.IsNullOrWhiteSpace(x.PhoneE164) ? (x.PhoneRaw ?? "") : x.PhoneE164,
                                  Name: x.Name)
                        );

                int successCount = 0, failureCount = 0;
                var now = DateTime.UtcNow;

                foreach (var r in recipients)
                {
                    var phone = PhoneOf(r);
                    string? name = r.Contact?.Name;

                    if (string.IsNullOrWhiteSpace(phone) && r.AudienceMemberId != null &&
                        audienceLookup.TryGetValue(r.AudienceMemberId.Value, out var a) &&
                        !string.IsNullOrWhiteSpace(a.Phone))
                    {
                        phone = a.Phone;
                        name ??= a.Name ?? "Customer";
                    }
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        failureCount++;
                        continue;
                    }

                    var runId = Guid.NewGuid();
                    var campaignSendLogId = Guid.NewGuid();

                    // ✅ Provider-specific component builder
                    bool built;
                    List<object> components;
                    string? buildErr;
                    if (providerUpper == "META_CLOUD")
                        built = BuildVideoTemplateComponents_Meta(videoUrl, templateMeta, r, out components, out buildErr);
                    else
                        built = BuildVideoTemplateComponents_Pinnacle(videoUrl, templateMeta, r, out components, out buildErr);

                    if (!built)
                    {
                        failureCount++;
                        _logger.LogWarning("[VideoTpl] Component build failed campaign={CampaignId} phone={Phone}: {Err}",
                            campaign.Id, phone, buildErr);

                        _context.CampaignSendLogs.Add(new CampaignSendLog
                        {
                            Id = campaignSendLogId,
                            CampaignId = campaign.Id,
                            BusinessId = businessId,
                            ContactId = r.ContactId,
                            RecipientId = r.Id,
                            MessageBody = campaign.MessageBody ?? templateKey,
                            TemplateId = templateKey,
                            SendStatus = "Failed",
                            ErrorMessage = $"component-build: {buildErr}",
                            CreatedAt = now,
                            CreatedBy = campaign.CreatedBy,
                            CTAFlowConfigId = campaign.CTAFlowConfigId,
                            CTAFlowStepId = entryStepId,
                            ButtonBundleJson = buttonBundleJson,
                            RunId = runId,
                            SourceChannel = "video_template"
                        });
                        continue;
                    }

                    var payload = new
                    {
                        messaging_product = "whatsapp",
                        to = phone,
                        type = "template",
                        template = new
                        {
                            name = templateKey,
                            language = new { code = languageCode },
                            components
                        }
                    };

                    // Attach recipient via stub to avoid accidental inserts of related entities
                    var recStub = new CampaignRecipient { Id = r.Id };
                    _context.CampaignRecipients.Attach(recStub);
                    recStub.MaterializedAt = recStub.MaterializedAt ?? now;
                    recStub.UpdatedAt = now;
                    recStub.IdempotencyKey = Idempotency.Sha256(
                        $"{campaign.Id}|{phone}|{templateKey}|{videoUrl}|{recStub.ResolvedParametersJson}|{recStub.ResolvedButtonUrlsJson}");

                    var result = await _messageEngineService.SendPayloadAsync(businessId, providerUpper, payload, phoneNumberIdOverride);

                    var logId = Guid.NewGuid();
                    _context.MessageLogs.Add(new MessageLog
                    {
                        Id = logId,
                        BusinessId = businessId,
                        CampaignId = campaign.Id,
                        ContactId = r.ContactId,
                        RecipientNumber = phone,
                        MessageContent = templateKey,
                        MediaUrl = videoUrl,
                        Status = result.Success ? "Sent" : "Failed",
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        RawResponse = result.RawResponse,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        Source = "campaign",
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId,
                        Provider = providerUpper,
                        ProviderMessageId = result.MessageId
                    });

                    await _billingIngest.IngestFromSendResponseAsync(
                        businessId: businessId,
                        messageLogId: logId,
                        provider: providerUpper,
                        rawResponseJson: result.RawResponse ?? "{}"
                    );

                    await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                    {
                        Id = campaignSendLogId,
                        CampaignId = campaign.Id,
                        BusinessId = businessId,
                        ContactId = r.ContactId,
                        RecipientId = r.Id,
                        MessageBody = campaign.MessageBody ?? templateKey,
                        TemplateId = templateKey,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        MessageLogId = logId,
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        CreatedBy = campaign.CreatedBy,
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId,
                        SourceChannel = "video_template"
                    });

                    if (result.Success) successCount++; else failureCount++;
                }

                await _context.SaveChangesAsync();
                return ResponseResult.SuccessInfo($"🎬 Video template sent to {successCount} recipients. ❌ Failed for {failureCount}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while sending video template campaign");
                return ResponseResult.ErrorInfo("🚨 Unexpected error while sending video campaign.", ex.ToString());
            }
        }

        

    //    private List<object> BuildVideoTemplateComponents(
    //        string provider, string headerVideoUrl,
    //        List<string> templateParams, List<CampaignButton>? buttonList,
    //        TemplateMetadataDto templateMeta, Guid campaignSendLogId,
    //        Contact contact, out List<string> resolvedButtonUrls)
    //    {
    //        // Reuse your current builders to get BODY + BUTTONS
    //        List<object> nonHeaderComponents;
    //        if (string.Equals(provider, "PINNACLE", StringComparison.Ordinal))
    //            nonHeaderComponents = BuildTextTemplateComponents_Pinnacle(
    //                templateParams, buttonList, templateMeta, campaignSendLogId, contact, out resolvedButtonUrls);
    //        else // META_CLOUD
    //            nonHeaderComponents = BuildTextTemplateComponents_Meta(
    //                templateParams, buttonList, templateMeta, campaignSendLogId, contact, out resolvedButtonUrls);

    //        // Prepend the HEADER/VIDEO piece (WhatsApp shape for both providers)
    //        var components = new List<object>
    //{
    //    new
    //    {
    //        type = "header",
    //        parameters = new object[] {
    //            new { type = "video", video = new { link = headerVideoUrl } }
    //        }
    //    }
    //};

    //        components.AddRange(nonHeaderComponents);
    //        return components;
    //    }
        private bool BuildVideoTemplateComponents_Meta(
            string videoUrl, TemplateMetadataDto templateMeta,
            CampaignRecipient r, out List<object> components, out string? error)
        {
            components = new List<object>();
            error = null;

            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                error = "required header VIDEO url is missing";
                return false;
            }

            // HEADER (video)
            components.Add(new Dictionary<string, object>
            {
                ["type"] = "header",
                ["parameters"] = new object[]
                {
            new Dictionary<string, object>
            {
                ["type"] = "video",
                ["video"] = new Dictionary<string, object>
                {
                    ["link"] = videoUrl
                }
            }
                }
            });

            // BODY {{1..N}}
            var count = Math.Max(0, templateMeta.PlaceholderCount);
            var bodyParams = DeserializeBodyParams(r.ResolvedParametersJson, count);
            if (count > 0)
            {
                // If template expects text params, enforce presence
                var missing = MissingIndices(bodyParams, count);
                if (missing.Count > 0)
                {
                    error = $"missing body params at {{ {string.Join(",", missing)} }}";
                    return false;
                }

                components.Add(new
                {
                    type = "body",
                    parameters = bodyParams.Select(p => new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = p ?? string.Empty
                    }).ToList()
                });
            }

            // URL BUTTON parameters (only when template declares dynamic pieces)
            if (templateMeta.ButtonParams != null && templateMeta.ButtonParams.Count > 0)
            {
                var urlDict = DeserializeButtonDict(r.ResolvedButtonUrlsJson);
                var total = Math.Min(3, templateMeta.ButtonParams.Count);

                for (int i = 0; i < total; i++)
                {
                    var bp = templateMeta.ButtonParams[i];
                    var subType = (bp.SubType ?? "url").ToLowerInvariant();
                    var paramMask = bp.ParameterValue?.Trim();

                    // Only dynamic URL buttons need a "text" parameter
                    if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var isDynamic = !string.IsNullOrWhiteSpace(paramMask) && paramMask.Contains("{{");
                    if (!isDynamic) continue;

                    // materializer persisted: button{1..3}.url_param
                    var key = $"button{i + 1}.url_param";
                    if (!urlDict.TryGetValue(key, out var dyn) || string.IsNullOrWhiteSpace(dyn))
                    {
                        error = $"missing dynamic URL param for {key}";
                        return false;
                    }

                    components.Add(new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["sub_type"] = "url",
                        ["index"] = i.ToString(), // "0","1","2"
                        ["parameters"] = new object[]
                        {
                    new Dictionary<string, object> { ["type"] = "text", ["text"] = dyn }
                        }
                    });
                }
            }

            return true;
        }

        private bool BuildVideoTemplateComponents_Pinnacle(
                string videoUrl,
                TemplateMetadataDto templateMeta,
                CampaignRecipient r,
        out List<object> components,
        out string? error)
        {
            // If Pinnacle uses same structure as Meta for templates, we can reuse Meta logic.
            // If they require a different header/media envelope, adapt here.
            return BuildVideoTemplateComponents_Meta(videoUrl, templateMeta, r, out components, out error);
        }
        private static Dictionary<string, string> DeserializeButtonDict(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : JsonConvert.DeserializeObject<Dictionary<string, string>>(json!)
                      ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        private static List<int> MissingIndices(List<string> bodyParams, int count)
        {
            var miss = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(i < bodyParams.Count ? bodyParams[i] : null))
                    miss.Add(i + 1); // 1-based for readability
            }
            return miss;
        }
        // ---------- helpers ----------
        private static List<string> DeserializeBodyParams(string? json, int expectedCount)
        {
            try
            {
                var arr = string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<string>()
                    : JsonConvert.DeserializeObject<string[]>(json!) ?? Array.Empty<string>();

                // pad/trim to template placeholder count
                var list = new List<string>(Enumerable.Repeat(string.Empty, Math.Max(expectedCount, 0)));
                for (int i = 0; i < Math.Min(expectedCount, arr.Length); i++)
                    list[i] = arr[i] ?? string.Empty;
                return list;
            }
            catch
            {
                return new List<string>(Enumerable.Repeat(string.Empty, Math.Max(expectedCount, 0)));
            }
        }
        private static readonly Regex PlaceholderRe = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);

        private string BuildTokenParam(Guid campaignSendLogId, int buttonIndex, string? buttonTitle, string destinationUrlAbsolute)
        {
            var full = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, buttonIndex, buttonTitle, destinationUrlAbsolute);
            var pos = full.LastIndexOf("/r/", StringComparison.OrdinalIgnoreCase);
            return (pos >= 0) ? full[(pos + 3)..] : full; // fallback: if not found, return full (rare)
        }

        private static string NormalizeAbsoluteUrlOrThrowForButton(string input, string buttonTitle, int buttonIndex)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException($"Destination is required for button '{buttonTitle}' (index {buttonIndex}).");

            // Trim + strip control chars
            var cleaned = new string(input.Trim().Where(c => !char.IsControl(c)).ToArray());
            if (cleaned.Length == 0)
                throw new ArgumentException($"Destination is required for button '{buttonTitle}' (index {buttonIndex}).");

            // Allow tel: and WhatsApp deep links
            if (cleaned.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("wa:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase))
            {
                return cleaned; // Accept as-is
            }

            // Normal web links
            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString();
            }

            // Reject everything else
            throw new ArgumentException(
                $"Destination must be an absolute http/https/tel/wa URL for button '{buttonTitle}' (index {buttonIndex}). Got: '{input}'");
        }

        private static bool LooksLikeAbsoluteBaseUrlWithPlaceholder(string? templateUrl)
        {
            if (string.IsNullOrWhiteSpace(templateUrl)) return false;
            var s = templateUrl.Trim();
            if (!s.Contains("{{")) return false;

            // Probe by replacing common placeholders with a char
            var probe = PlaceholderRe.Replace(s, "x");
            return Uri.TryCreate(probe, UriKind.Absolute, out var abs) &&
                   (abs.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    abs.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private static object[] BuildBodyParameters(List<string>? templateParams, int requiredCount)
        {
            if (requiredCount <= 0) return Array.Empty<object>();

            var src = templateParams ?? new List<string>();
            if (src.Count > requiredCount) src = src.Take(requiredCount).ToList();
            while (src.Count < requiredCount) src.Add(string.Empty);

            return src.Select(p => (object)new { type = "text", text = p ?? string.Empty }).ToArray();
        }

        private static string NormalizePhoneForTel(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var p = raw.Trim();
            var digits = new string(p.Where(char.IsDigit).ToArray());
            // keep leading + if present initially; always output +<digits>
            return "+" + digits;
        }

        private static string ReplaceAllPlaceholdersWith(string template, string replacement)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            return PlaceholderRe.Replace(template, _ => replacement ?? string.Empty);
        }

        // ======================================================
        // META — TEXT TEMPLATE COMPONENTS
        // ======================================================

        // Back-compat wrapper (old signature)
        //private List<object> BuildTextTemplateComponents_Meta(
        //    List<string> templateParams,
        //    List<CampaignButton>? buttonList,
        //    TemplateMetadataDto templateMeta,
        //    Guid campaignSendLogId,
        //    Contact contact)
        //{
        //    return BuildTextTemplateComponents_Meta(
        //        templateParams, buttonList, templateMeta, campaignSendLogId, contact, out _);
        //}

        // New overload with resolvedButtonUrls
        private List<object> BuildTextTemplateComponents_Meta(
            List<string> templateParams,
            List<CampaignButton>? buttonList,
            TemplateMetadataDto templateMeta,
            Guid campaignSendLogId,
            Contact contact,
            out List<string> resolvedButtonUrls)
        {
            var components = new List<object>();
            resolvedButtonUrls = new List<string>();

            // BODY: send exactly PlaceholderCount
            if (templateMeta.PlaceholderCount > 0)
            {
                var bodyParams = BuildBodyParameters(templateParams, templateMeta.PlaceholderCount);
                components.Add(new { type = "body", parameters = bodyParams });
            }

            // No buttons or template has no button params
            if (buttonList == null || buttonList.Count == 0 ||
                templateMeta.ButtonParams == null || templateMeta.ButtonParams.Count == 0)
                return components;

            // Ensure index alignment with the template by ordering by Position (then original index)
            var orderedButtons = buttonList
                .Select((b, idx) => new { Btn = b, idx })
                .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                .ThenBy(x => x.idx)
                .Select(x => x.Btn)
                .ToList();

            var total = Math.Min(3, Math.Min(orderedButtons.Count, templateMeta.ButtonParams.Count));

            // Phone normalization (for optional {{1}} substitution on campaign button value)
            var phone = NormalizePhoneForTel(contact?.PhoneNumber);
            var encodedPhone = Uri.EscapeDataString(phone);

            for (int i = 0; i < total; i++)
            {
                var meta = templateMeta.ButtonParams[i];
                var subType = (meta.SubType ?? "url").ToLowerInvariant();
                var metaParam = meta.ParameterValue?.Trim();

                // Meta needs parameters ONLY for dynamic URL buttons
                if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase))
                    continue;

                var isDynamic = !string.IsNullOrWhiteSpace(metaParam) && metaParam.Contains("{{");
                if (!isDynamic)
                    continue;

                var btn = orderedButtons[i];
                var btnType = (btn?.Type ?? "URL").ToUpperInvariant();
                if (!string.Equals(btnType, "URL", StringComparison.OrdinalIgnoreCase))
                {
                    // If template expects dynamic URL at this index and your campaign button isn't URL, skip to avoid provider error
                    continue;
                }

                var valueRaw = btn.Value?.Trim();
                if (string.IsNullOrWhiteSpace(valueRaw))
                {
                    throw new InvalidOperationException(
                        $"Template requires a dynamic URL at button index {i}, but campaign button value is empty.");
                }

                // Optional phone substitution in destination (support any {{n}})
                var resolvedDestination = PlaceholderRe.Replace(valueRaw, m =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out var n)) return "";
                    if (n == 1) return encodedPhone; // convention: {{1}} can be phone
                    var idx = n - 1;
                    return (idx >= 0 && idx < templateParams.Count) ? (templateParams[idx] ?? "") : "";
                });

                resolvedDestination = NormalizeAbsoluteUrlOrThrowForButton(resolvedDestination, btn.Title ?? "", i);

                // Build both; choose which to send based on template base style
                var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, i, btn.Title, resolvedDestination);
                var tokenParam = BuildTokenParam(campaignSendLogId, i, btn.Title, resolvedDestination);

                var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
                var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

                components.Add(new Dictionary<string, object>
                {
                    ["type"] = "button",
                    ["sub_type"] = "url",
                    ["index"] = i.ToString(), // "0"/"1"/"2"
                    ["parameters"] = new[] {
                new Dictionary<string, object> { ["type"] = "text", ["text"] = valueToSend }
            }
                });

                // Provider-resolved URL (what the client actually clicks):
                // replace all placeholders in provider template with the parameter we sent.
                var providerResolved = ReplaceAllPlaceholdersWith(metaParam ?? "", valueToSend);
                resolvedButtonUrls.Add(providerResolved);
            }

            return components;
        }

        // ======================================================
        // PINNACLE — TEXT TEMPLATE COMPONENTS
        // ======================================================

        // Back-compat wrapper (old signature)
        private List<object> BuildTextTemplateComponents_Pinnacle(
            List<string> templateParams,
            List<CampaignButton>? buttonList,
            TemplateMetadataDto templateMeta,
            Guid campaignSendLogId,
            Contact contact)
        {
            return BuildTextTemplateComponents_Pinnacle(
                templateParams, buttonList, templateMeta, campaignSendLogId, contact, out _);
        }

        // New overload with resolvedButtonUrls
        private List<object> BuildTextTemplateComponents_Pinnacle(
            List<string> templateParams,
            List<CampaignButton>? buttonList,
            TemplateMetadataDto templateMeta,
            Guid campaignSendLogId,
            Contact contact,
            out List<string> resolvedButtonUrls)
        {
            var components = new List<object>();
            resolvedButtonUrls = new List<string>();

            // BODY: Pinnacle is strict → always send exactly PlaceholderCount
            if (templateMeta.PlaceholderCount > 0)
            {
                var bodyParams = BuildBodyParameters(templateParams, templateMeta.PlaceholderCount);
                components.Add(new { type = "body", parameters = bodyParams });
            }

            // No buttons to map → return body-only
            if (buttonList == null || buttonList.Count == 0 ||
                templateMeta.ButtonParams == null || templateMeta.ButtonParams.Count == 0)
                return components;

            // Ensure index alignment with the template by ordering by Position (then original index)
            var orderedButtons = buttonList
                .Select((b, idx) => new { Btn = b, idx })
                .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                .ThenBy(x => x.idx)
                .Select(x => x.Btn)
                .ToList();

            var total = Math.Min(3, Math.Min(orderedButtons.Count, templateMeta.ButtonParams.Count));

            // Phone normalization (for optional {{1}} substitution on campaign button value)
            var phone = NormalizePhoneForTel(contact?.PhoneNumber);
            var encodedPhone = Uri.EscapeDataString(phone);

            for (int i = 0; i < total; i++)
            {
                var meta = templateMeta.ButtonParams[i];
                var subType = (meta.SubType ?? "url").ToLowerInvariant();
                var metaParam = meta.ParameterValue?.Trim();

                // This path supports dynamic URL params only
                if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase))
                    continue;

                var isDynamic = !string.IsNullOrWhiteSpace(metaParam) && metaParam.Contains("{{");
                if (!isDynamic)
                    continue;

                var btn = orderedButtons[i];
                var btnType = (btn?.Type ?? "URL").ToUpperInvariant();
                if (!string.Equals(btnType, "URL", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Template expects a dynamic URL at button index {i}, but campaign button type is '{btn?.Type}'.");
                }

                var valueRaw = btn?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(valueRaw))
                {
                    throw new InvalidOperationException(
                        $"Template requires a dynamic URL at button index {i}, but campaign button value is empty.");
                }

                // Optional phone + param substitution (support any {{n}})
                var resolvedDestination = PlaceholderRe.Replace(valueRaw, m =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out var n)) return "";
                    if (n == 1) return encodedPhone;
                    var idx = n - 1;
                    return (idx >= 0 && idx < templateParams.Count) ? (templateParams[idx] ?? "") : "";
                });

                // Validate + normalize absolute URL
                resolvedDestination = NormalizeAbsoluteUrlOrThrowForButton(resolvedDestination, btn!.Title ?? "", i);

                // Build both options: full tracked URL vs token param (for absolute-base placeholders)
                var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, i, btn.Title, resolvedDestination);
                var tokenParam = BuildTokenParam(campaignSendLogId, i, btn.Title, resolvedDestination);

                var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
                var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

                // Pinnacle payload shape (kept aligned with Meta)
                components.Add(new Dictionary<string, object>
                {
                    ["type"] = "button",
                    ["sub_type"] = "url",
                    ["index"] = i.ToString(),
                    ["parameters"] = new[] {
                new Dictionary<string, object> { ["type"] = "text", ["text"] = valueToSend }
            }
                });

                // Provider-resolved URL (what the user will open)
                var providerResolved = ReplaceAllPlaceholdersWith(metaParam ?? "", valueToSend);
                resolvedButtonUrls.Add(providerResolved);
            }

            return components;
        }

        #region SendImagetemplate
        public async Task<ResponseResult> SendImageTemplateCampaignAsync(Campaign campaign)
        {
            try
            {
                if (campaign == null || campaign.IsDeleted)
                    return ResponseResult.ErrorInfo("❌ Invalid campaign.");
                if (campaign.Recipients == null || campaign.Recipients.Count == 0)
                    return ResponseResult.ErrorInfo("❌ No recipients to send.");

                var businessId = campaign.BusinessId;

                static string? ResolveRecipientPhone(CampaignRecipient r) =>
                    r?.Contact?.PhoneNumber ?? r?.AudienceMember?.PhoneE164 ?? r?.AudienceMember?.PhoneRaw;

                var recipients = campaign.Recipients
                    .Where(r => !string.IsNullOrWhiteSpace(ResolveRecipientPhone(r)))
                    .ToList();

                if (recipients.Count == 0)
                    return ResponseResult.ErrorInfo("⚠️ No valid recipients with phone numbers (Contact/AudienceMember).");

                // Flow/template selection
                var (_, entryTemplate) = await ResolveFlowEntryAsync(businessId, campaign.CTAFlowConfigId);
                var templateKey = !string.IsNullOrWhiteSpace(entryTemplate)
                    ? entryTemplate!.Trim()
                    : (campaign.TemplateId ?? campaign.MessageTemplate ?? "").Trim();

                if (string.IsNullOrWhiteSpace(templateKey))
                    return ResponseResult.ErrorInfo("❌ No template selected.");

                // WhatsApp settings
                var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(businessId);
                if (wa is null)
                    return ResponseResult.ErrorInfo("❌ WhatsApp settings not found.");

                // DB stores Provider in UPPERCASE
                var providerUpper = (wa.Provider ?? string.Empty).Trim().ToUpperInvariant();
                if (providerUpper != "META_CLOUD" && providerUpper != "PINNACLE")
                    return ResponseResult.ErrorInfo($"❌ Unsupported provider configured: {providerUpper}");

                // Template from DB (try TemplateId first, then Name)
                WhatsAppTemplate? templateRow = await _context.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId &&
                                t.IsActive &&
                                t.Provider == providerUpper &&
                                t.TemplateId == templateKey)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.LastSyncedAt)
                    .FirstOrDefaultAsync();

                if (templateRow is null)
                {
                    templateRow = await _context.WhatsAppTemplates
                        .AsNoTracking()
                        .Where(t => t.BusinessId == businessId &&
                                    t.IsActive &&
                                    t.Provider == providerUpper &&
                                    t.Name == templateKey)
                        .OrderByDescending(t => t.UpdatedAt)
                        .ThenByDescending(t => t.LastSyncedAt)
                        .FirstOrDefaultAsync();
                }

                if (templateRow == null)
                    return ResponseResult.ErrorInfo("❌ Template metadata not found in DB.");

                // Map DB row -> DTO your builders need
                var tmplMeta = new TemplateMetadataDto
                {
                    Name = templateRow.Name,
                    Language = templateRow.LanguageCode,
                    Body = templateRow.Body,
                    // FINAL MODEL: body-only placeholder count:
                    PlaceholderCount = Math.Max(0, templateRow.BodyVarCount),
                    ButtonParams = ParseUrlButtonsToButtonMeta(templateRow.UrlButtons)

                };
                var parameterFormat = (templateRow.ParameterFormat ?? "POSITIONAL").Trim().ToUpperInvariant();
                var bodyParamNames = ExtractBodyParamNamesFromTemplate(templateRow.RawJson, templateRow.Body, parameterFormat);
                var languageCode = (tmplMeta.Language ?? "").Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                    return ResponseResult.ErrorInfo("❌ Template language missing in DB.");

                // Sender (campaign override → DTO)
                string? phoneNumberIdOverride = !string.IsNullOrWhiteSpace(campaign.PhoneNumberId)
                    ? campaign.PhoneNumberId
                    : wa.PhoneNumberId;

                if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberIdOverride))
                    return ResponseResult.ErrorInfo("❌ No PhoneNumberId configured for Meta Cloud sender.");

                // Flow entry step id
                Guid? entryStepId = null;
                if (campaign.CTAFlowConfigId.HasValue)
                {
                    entryStepId = await _context.CTAFlowSteps
                        .Where(s => s.CTAFlowConfigId == campaign.CTAFlowConfigId.Value)
                        .OrderBy(s => s.StepOrder)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync();
                }

                // Freeze button bundle (for analytics)
                string? buttonBundleJson = null;
                if (tmplMeta.ButtonParams is { Count: > 0 })
                {
                    var bundle = tmplMeta.ButtonParams
                        .OrderBy(b => b.Index)
                        .Take(3)
                        .Select((b, i) => new { i, position = i + 1, text = (b.Text ?? "").Trim(), type = b.Type, subType = b.SubType })
                        .ToList();
                    buttonBundleJson = JsonConvert.SerializeObject(bundle);
                }

                // Prefetch AudienceMembers for recipients without Contact
                var neededMemberIds = recipients
                    .Where(x => x.ContactId == null && x.AudienceMemberId != null)
                    .Select(x => x.AudienceMemberId!.Value)
                    .Distinct()
                    .ToList();

                var audienceLookup = neededMemberIds.Count == 0
                    ? new Dictionary<Guid, (string Phone, string? Name)>()
                    : await _context.AudienceMembers.AsNoTracking()
                        .Where(m => m.BusinessId == businessId && neededMemberIds.Contains(m.Id))
                        .Select(m => new { m.Id, m.PhoneE164, m.PhoneRaw, m.Name })
                        .ToDictionaryAsync(
                            x => x.Id,
                            x => (Phone: string.IsNullOrWhiteSpace(x.PhoneE164) ? (x.PhoneRaw ?? "") : x.PhoneE164,
                                  Name: x.Name)
                        );

                // Campaign buttons (stable order)
                var buttons = campaign.MultiButtons?
                    .Select((b, idx) => new { Btn = b, idx })
                    .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                    .ThenBy(x => x.idx)
                    .Select(x => x.Btn)
                    .ToList() ?? new List<CampaignButton>();

                int successCount = 0, failureCount = 0;
                var now = DateTime.UtcNow;

                foreach (var r in recipients)
                {
                    var phone = ResolveRecipientPhone(r);
                    string? name = r.Contact?.Name;

                    if (string.IsNullOrWhiteSpace(phone) && r.AudienceMemberId != null &&
                        audienceLookup.TryGetValue(r.AudienceMemberId.Value, out var a) &&
                        !string.IsNullOrWhiteSpace(a.Phone))
                    {
                        phone = a.Phone;
                        name ??= a.Name ?? "Customer";
                    }

                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        failureCount++;
                        continue;
                    }

                    // Synthetic contact for templating only
                    var contactForTemplating = r.Contact ?? new Contact
                    {
                        Id = Guid.Empty,
                        BusinessId = businessId,
                        PhoneNumber = phone,
                        Name = name ?? "Customer"
                    };

                    // BODY params per recipient (positional only; your final model’s hot path)
                    //var resolvedParams = GetRecipientBodyParams(r, tmplMeta.PlaceholderCount, campaign.TemplateParameters);
                    var resolvedParams = GetRecipientBodyParams(
                           r,
                           tmplMeta.PlaceholderCount, campaign.TemplateParameters, bodyParamNames, parameterFormat
                        );
                    if (tmplMeta.PlaceholderCount > 0 && resolvedParams.Any(string.IsNullOrWhiteSpace))
                    {
                        failureCount++;
                        var why = $"Missing body parameter(s): expected {tmplMeta.PlaceholderCount}, got {resolvedParams.Count(x => !string.IsNullOrWhiteSpace(x))} filled.";

                        var recStubMiss = new CampaignRecipient { Id = r.Id };
                        _context.CampaignRecipients.Attach(recStubMiss);
                        recStubMiss.MaterializedAt = now;
                        recStubMiss.UpdatedAt = now;
                        recStubMiss.ResolvedParametersJson = JsonConvert.SerializeObject(resolvedParams);

                        var logIdLocal = Guid.NewGuid();
                        _context.MessageLogs.Add(new MessageLog
                        {
                            Id = logIdLocal,
                            BusinessId = businessId,
                            CampaignId = campaign.Id,
                            ContactId = r.ContactId,
                            RecipientNumber = phone,
                            MessageContent = templateKey,
                            MediaUrl = campaign.ImageUrl,
                            Status = "Failed",
                            ErrorMessage = why,
                            RawResponse = "{\"local_error\":\"missing_template_body_params\"}",
                            CreatedAt = now,
                            Source = "campaign",
                            CTAFlowConfigId = campaign.CTAFlowConfigId,
                            CTAFlowStepId = entryStepId,
                            ButtonBundleJson = buttonBundleJson
                        });

                        await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaign.Id,
                            BusinessId = businessId,
                            ContactId = r.ContactId,
                            RecipientId = r.Id,
                            MessageBody = campaign.MessageBody ?? templateKey,
                            TemplateId = templateKey,
                            SendStatus = "Failed",
                            MessageLogId = logIdLocal,
                            ErrorMessage = why,
                            CreatedAt = now,
                            CTAFlowConfigId = campaign.CTAFlowConfigId,
                            CTAFlowStepId = entryStepId,
                            ButtonBundleJson = buttonBundleJson
                        });

                        continue;
                    }

                    // Build components and resolve dynamic URL buttons
                    var runId = Guid.NewGuid();
                    var campaignSendLogId = Guid.NewGuid();
                    List<string> resolvedButtonUrls;

                    _ = (providerUpper == "PINNACLE")
                        ? BuildImageTemplateComponents_Pinnacle(
                            campaign.ImageUrl, resolvedParams, buttons, tmplMeta, campaignSendLogId, contactForTemplating, out resolvedButtonUrls)
                        : BuildImageTemplateComponents_Meta(
                            campaign.ImageUrl, resolvedParams, buttons, tmplMeta, campaignSendLogId, contactForTemplating, out resolvedButtonUrls);

                    // Attach recipient via stub before send
                    var recStub = new CampaignRecipient { Id = r.Id };
                    _context.CampaignRecipients.Attach(recStub);
                    recStub.ResolvedParametersJson = JsonConvert.SerializeObject(resolvedParams);
                    recStub.ResolvedButtonUrlsJson = JsonConvert.SerializeObject(resolvedButtonUrls);
                    recStub.MaterializedAt = now;
                    recStub.UpdatedAt = now;
                    recStub.IdempotencyKey = Idempotency.Sha256(
                        $"{campaign.Id}|{phone}|{templateKey}|{recStub.ResolvedParametersJson}|{recStub.ResolvedButtonUrlsJson}|{campaign.ImageUrl}|{campaign.ImageCaption}");

                    // Send via engine
                    var dto = new ImageTemplateMessageDto
                    {
                        BusinessId = businessId,
                        Provider = providerUpper,
                        PhoneNumberId = phoneNumberIdOverride,
                        RecipientNumber = phone,
                        TemplateName = templateKey,
                        LanguageCode = languageCode,
                        HeaderImageUrl = campaign.ImageUrl,
                        TemplateBody = campaign.MessageBody,
                        TemplateParameters = resolvedParams,
                        ButtonParameters = buttons.Take(3).Select(b => new CampaignButtonDto
                        {
                            ButtonText = b.Title,
                            ButtonType = b.Type,
                            TargetUrl = b.Value
                        }).ToList(),
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId
                    };

                    var result = await _messageEngineService.SendImageTemplateMessageAsync(dto, businessId);

                    var logId = Guid.NewGuid();
                    _context.MessageLogs.Add(new MessageLog
                    {
                        Id = logId,
                        BusinessId = businessId,
                        CampaignId = campaign.Id,
                        ContactId = r.ContactId,
                        RecipientNumber = phone,
                        MessageContent = templateKey,
                        MediaUrl = campaign.ImageUrl,
                        Status = result.Success ? "Sent" : "Failed",
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        RawResponse = result.RawResponse,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        Source = "campaign",
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId
                    });

                    await _billingIngest.IngestFromSendResponseAsync(
                        businessId: businessId,
                        messageLogId: logId,
                        provider: providerUpper,
                        rawResponseJson: result.RawResponse ?? "{}"
                    );

                    await _context.CampaignSendLogs.AddAsync(new CampaignSendLog
                    {
                        Id = campaignSendLogId,
                        CampaignId = campaign.Id,
                        BusinessId = businessId,
                        ContactId = r.ContactId,
                        RecipientId = r.Id,
                        MessageBody = campaign.MessageBody ?? templateKey,
                        TemplateId = templateKey,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        MessageLogId = logId,
                        MessageId = result.MessageId,
                        ErrorMessage = result.ErrorMessage,
                        CreatedAt = now,
                        SentAt = result.Success ? now : (DateTime?)null,
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        CTAFlowStepId = entryStepId,
                        ButtonBundleJson = buttonBundleJson,
                        RunId = runId
                    });

                    if (result.Success) successCount++; else failureCount++;
                }

                await _context.SaveChangesAsync();
                return ResponseResult.SuccessInfo($"📤 Sent to {successCount} recipients. ❌ Failed for {failureCount}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while sending image template campaign");
                return ResponseResult.ErrorInfo("🚨 Unexpected error while sending campaign.", ex.ToString());
            }
        }

        // Storage shape [{ index, parameters: [...] }] → your ButtonMetadataDto

        private List<object> BuildImageTemplateComponents_Pinnacle(
       string? imageUrl,
       List<string> templateParams,
       List<CampaignButton>? buttonList,
       TemplateMetadataDto templateMeta,
       Guid campaignSendLogId,
       Contact contact,
       out List<string> resolvedButtonUrls)
        {
            var components = new List<object>();
            resolvedButtonUrls = new List<string>();

            // Header
            if (!string.IsNullOrWhiteSpace(imageUrl) && templateMeta.HasImageHeader)
            {
                components.Add(new
                {
                    type = "header",
                    parameters = new object[]
                    {
                new { type = "image", image = new { link = imageUrl } }
                    }
                });
            }

            // Body
            if (templateMeta.PlaceholderCount > 0 && templateParams?.Count > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = templateParams.Select(p => new { type = "text", text = p }).ToArray()
                });
            }

            // Buttons
            if (buttonList == null || buttonList.Count == 0 ||
                templateMeta.ButtonParams == null || templateMeta.ButtonParams.Count == 0)
                return components;

            var total = Math.Min(3, Math.Min(buttonList.Count, templateMeta.ButtonParams.Count));

            // phone for optional {{1}}
            var phone = string.IsNullOrWhiteSpace(contact?.PhoneNumber) ? "" :
                        (contact.PhoneNumber.StartsWith("+") ? contact.PhoneNumber : "+" + contact.PhoneNumber);
            var encodedPhone = Uri.EscapeDataString(phone);

            for (int i = 0; i < total; i++)
            {
                var btn = buttonList[i];
                var meta = templateMeta.ButtonParams[i];
                var subtype = (meta.SubType ?? "url").ToLowerInvariant();
                var metaParam = meta.ParameterValue?.Trim() ?? string.Empty; // e.g. "/r/{{1}}"
                var isDynamic = metaParam.Contains("{{");

                if (!isDynamic)
                {
                    // static provider button at this index — no parameters to send
                    components.Add(new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["sub_type"] = subtype,
                        ["index"] = i
                    });
                    continue;
                }

                var valueRaw = btn?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(valueRaw)) continue;

                // Optional phone substitution + body params {{n}}
                var resolvedDestination = PlaceholderRe.Replace(valueRaw, m =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out var n)) return "";
                    if (n == 1) return encodedPhone;
                    var idx = n - 1;
                    return (idx >= 0 && idx < templateParams.Count) ? (templateParams[idx] ?? "") : "";
                });

                // Track + token (same pattern as text path)
                var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, i, btn.Title, resolvedDestination);
                var tokenParam = BuildTokenParam(campaignSendLogId, i, btn.Title, resolvedDestination);

                var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
                var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

                components.Add(new Dictionary<string, object>
                {
                    ["type"] = "button",
                    ["sub_type"] = subtype,
                    ["index"] = i,
                    ["parameters"] = new[] { new Dictionary<string, object> { ["type"] = "text", ["text"] = valueToSend } }
                });

                // what the client will actually open once provider composes the URL
                var providerResolved = ReplaceAllPlaceholdersWith(metaParam, valueToSend);
                resolvedButtonUrls.Add(providerResolved);
            }

            return components;
        }


        private List<object> BuildImageTemplateComponents_Meta(
       string? imageUrl,
       List<string> templateParams,
       List<CampaignButton>? buttonList,
       TemplateMetadataDto templateMeta,
       Guid campaignSendLogId,
       Contact contact,
       out List<string> resolvedButtonUrls)
        {
            var components = new List<object>();
            resolvedButtonUrls = new List<string>();

            // Header
            if (!string.IsNullOrWhiteSpace(imageUrl) && templateMeta.HasImageHeader)
            {
                components.Add(new
                {
                    type = "header",
                    parameters = new[]
                    {
                new { type = "image", image = new { link = imageUrl } }
            }
                });
            }

            // Body
            if (templateMeta.PlaceholderCount > 0 && templateParams?.Count > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = templateParams.Select(p => new { type = "text", text = p }).ToArray()
                });
            }

            // Dynamic URL buttons only
            if (buttonList == null || buttonList.Count == 0 ||
                templateMeta.ButtonParams == null || templateMeta.ButtonParams.Count == 0)
                return components;

            var total = Math.Min(3, Math.Min(buttonList.Count, (templateMeta.ButtonParams?.Count() ?? 0)));
            var phone = string.IsNullOrWhiteSpace(contact?.PhoneNumber) ? "" :
                        (contact.PhoneNumber.StartsWith("+") ? contact.PhoneNumber : "+" + contact.PhoneNumber);
            var encodedPhone = Uri.EscapeDataString(phone);

            for (int i = 0; i < total; i++)
            {
                var meta = templateMeta.ButtonParams[i];
                var metaParam = meta.ParameterValue?.Trim();
                var isDynamic = !string.IsNullOrWhiteSpace(metaParam) && metaParam.Contains("{{");
                if (!isDynamic) continue;

                var btn = buttonList[i];
                var valueRaw = btn.Value?.Trim();
                if (string.IsNullOrWhiteSpace(valueRaw)) continue;

                var subtype = (meta.SubType ?? "url").ToLowerInvariant();

                // {{n}} substitution ({{1}} := phone)
                var resolvedDestination = PlaceholderRe.Replace(valueRaw, m =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out var n)) return "";
                    if (n == 1) return encodedPhone;
                    var idx = n - 1;
                    return (idx >= 0 && idx < templateParams.Count) ? (templateParams[idx] ?? "") : "";
                });

                var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, i, btn.Title, resolvedDestination);
                var tokenParam = BuildTokenParam(campaignSendLogId, i, btn.Title, resolvedDestination);

                var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
                var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

                components.Add(new Dictionary<string, object>
                {
                    ["type"] = "button",
                    ["sub_type"] = subtype,      // "url"
                    ["index"] = i.ToString(), // "0"/"1"/"2" for Meta
                    ["parameters"] = new[] { new Dictionary<string, object> { ["type"] = "text", ["text"] = valueToSend } }
                });

                var providerResolved = ReplaceAllPlaceholdersWith(metaParam ?? "", valueToSend);
                resolvedButtonUrls.Add(providerResolved);
            }

            return components;
        }


        private List<object> BuildVideoTemplateComponents_Pinnacle(
            string? videoUrl,
            List<string> templateParams,
            List<CampaignButton>? buttonList,
            TemplateMetadataDto templateMeta,
            Guid campaignSendLogId,
            Contact contact)
        {
            var components = new List<object>();

            // --- Header (VIDEO) ---
            // TemplateMetadataDto has no HeaderType/HasVideoHeader → emit header when URL is present.
            if (!string.IsNullOrWhiteSpace(videoUrl))
            {
                components.Add(new
                {
                    type = "header",
                    parameters = new object[]
                    {
                new { type = "video", video = new { link = videoUrl } }
                    }
                });
            }

            // --- Body ---
            var bodyCount = templateMeta?.PlaceholderCount ?? 0;
            if (templateParams != null && templateParams.Count > 0 && bodyCount > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = templateParams.Select(p => new { type = "text", text = p ?? string.Empty }).ToArray()
                });
            }

            // --- Buttons (URL buttons only; indexes 0..2) ---
            if (buttonList != null && buttonList.Count > 0)
            {
                components.AddRange(BuildPinnacleUrlButtons(buttonList));
            }

            return components;
        }

        // Works with either CampaignButton (Type/Value) or CampaignButtonDto (ButtonType/TargetUrl).
        private static IEnumerable<object> BuildPinnacleUrlButtons(IEnumerable<object> rawButtons)
        {
            // keep incoming order; cap at 3
            var ordered = (rawButtons ?? Enumerable.Empty<object>()).Take(3).ToList();
            var n = ordered is ICollection<object> col ? col.Count : ordered.Count();

            for (int i = 0; i < n; i++)
            {
                var b = ordered[i];

                // Try to read "Type" or "ButtonType"
                var typeProp = b.GetType().GetProperty("Type") ?? b.GetType().GetProperty("ButtonType");
                var typeVal = (typeProp?.GetValue(b) as string)?.Trim().ToLowerInvariant() ?? "url";
                if (typeVal != "url") continue;

                // Try to read "Value" (CampaignButton) or "TargetUrl" (CampaignButtonDto)
                var valueProp = b.GetType().GetProperty("Value") ?? b.GetType().GetProperty("TargetUrl");
                var paramText = (valueProp?.GetValue(b) as string) ?? string.Empty;

                // If there is a per-recipient URL param, include it; otherwise emit static URL button (no parameters).
                if (!string.IsNullOrWhiteSpace(paramText))
                {
                    yield return new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i, // 0-based
                        parameters = new object[]
                        {
                    new { type = "text", text = paramText }
                        }
                    };
                }
                else
                {
                    yield return new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i
                    };
                }
            }
        }

        private static List<object> BuildVideoTemplateComponents_Meta(
                string? videoUrl,
                List<string>? templateParams,
                List<CampaignButtonDto>? buttonParams,
                TemplateMetadataDto? templateMeta)
        {
            var components = new List<object>();

            // We’re in the VIDEO sender path, so add header only if a URL is present.
            if (!string.IsNullOrWhiteSpace(videoUrl))
            {
                components.Add(new
                {
                    type = "header",
                    parameters = new object[]
                    {
                new { type = "video", video = new { link = videoUrl } }
                    }
                });
            }

            // Body placeholders: use meta.PlaceholderCount if available, otherwise list length.
            var bodyCount = templateMeta?.PlaceholderCount ?? templateParams?.Count ?? 0;
            if (bodyCount > 0 && (templateParams?.Count ?? 0) > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = templateParams!.Select(p => new { type = "text", text = p ?? string.Empty }).ToArray()
                });
            }

            // Buttons (URL buttons only). See helper below.
            if (buttonParams != null && buttonParams.Count > 0)
            {
                components.AddRange(BuildMetaTemplateButtons(buttonParams, templateMeta));
            }

            return components;
        }

        private static IEnumerable<object> BuildMetaTemplateButtons(
            List<CampaignButtonDto> buttons,
            TemplateMetadataDto? templateMeta)   // meta unused here; kept for future expansion
        {
            // Keep incoming order; cap at 3
            var ordered = (buttons ?? new List<CampaignButtonDto>())
                .Take(3)
                .ToList();

            // Avoid Count ambiguity by caching n
            int n = ordered is ICollection<CampaignButtonDto> col ? col.Count : ordered.Count();

            for (int i = 0; i < n; i++)
            {
                var b = ordered[i];

                // Only URL buttons are supported for parameterized Meta buttons
                var isUrl = string.Equals(b?.ButtonType, "url", StringComparison.OrdinalIgnoreCase);
                if (!isUrl) continue;

                // If we have a per-recipient param (TargetUrl), include a parameter; else emit static button
                var paramText = b?.TargetUrl ?? string.Empty;
                var needsParam = !string.IsNullOrWhiteSpace(paramText);

                if (needsParam)
                {
                    yield return new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i, // Meta uses 0-based indexes
                        parameters = new object[]
                        {
                    new { type = "text", text = paramText }
                        }
                    };
                }
                else
                {
                    yield return new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i
                    };
                }
            }
        }


        public async Task<List<FlowListItemDto>> GetAvailableFlowsAsync(Guid businessId, bool onlyPublished = true)
        {
            return await _context.CTAFlowConfigs
                .AsNoTracking()
                .Where(f => f.BusinessId == businessId && f.IsActive && (!onlyPublished || f.IsPublished))
                .OrderByDescending(f => f.UpdatedAt)
                .Select(f => new FlowListItemDto
                {
                    Id = f.Id,
                    FlowName = f.FlowName,
                    IsPublished = f.IsPublished
                })
                .ToListAsync();
        }
        // ===================== DRY RUN (Step 2.3) =====================

        public async Task<CampaignDryRunResponseDto> DryRunTemplateCampaignAsync(Guid campaignId, int maxRecipients = 20)
        {
            var resp = new CampaignDryRunResponseDto { CampaignId = campaignId };

            // Load campaign + recipients (+Contact +AudienceMember) + buttons
            var campaign = await _context.Campaigns
                .Include(c => c.Recipients).ThenInclude(r => r.Contact)
                .Include(c => c.Recipients).ThenInclude(r => r.AudienceMember)
                .Include(c => c.MultiButtons)
                .FirstOrDefaultAsync(c => c.Id == campaignId && !c.IsDeleted);

            if (campaign == null)
            {
                resp.Notes.Add("Campaign not found.");
                return resp;
            }

            resp.CampaignType = campaign.CampaignType ?? "text";

            // Resolve entry template name from flow if present, else fall back
            var (_, entryTemplate) = await ResolveFlowEntryAsync(campaign.BusinessId, campaign.CTAFlowConfigId);
            var templateName = !string.IsNullOrWhiteSpace(entryTemplate)
                ? entryTemplate!
                : (campaign.TemplateId ?? campaign.MessageTemplate ?? "");

            if (string.IsNullOrWhiteSpace(templateName))
            {
                resp.Notes.Add("Template name is missing.");
                return resp;
            }

            // Fetch provider template metadata once (language, placeholders, button schema)
            var templateMeta = await _templateFetcherService.GetTemplateByNameAsync(
                campaign.BusinessId, templateName, includeButtons: true);

            resp.TemplateName = templateName;

            if (templateMeta == null)
            {
                resp.Notes.Add($"Template metadata not found for business. Name='{templateName}'.");
                return resp;
            }

            resp.Language = (templateMeta.Language ?? "").Trim();
            resp.HasHeaderMedia = templateMeta.HasImageHeader;

            if (string.IsNullOrWhiteSpace(resp.Language))
                resp.Notes.Add("Template language is not specified on metadata.");

            // Ensure non-null param list for builders (snapshot provided params)
            var providedParams = TemplateParameterHelper.ParseTemplateParams(campaign.TemplateParameters)
                                 ?? new List<string>();

            resp.RequiredPlaceholders = Math.Max(0, templateMeta.PlaceholderCount);
            resp.ProvidedPlaceholders = providedParams.Count;

            if (resp.RequiredPlaceholders != resp.ProvidedPlaceholders)
                resp.Notes.Add($"Placeholder mismatch: template requires {resp.RequiredPlaceholders}, provided {resp.ProvidedPlaceholders}. Consider re-snapshotting parameters.");

            // Dynamic URL button check (template expects params) vs campaign button values
            var templButtons = templateMeta.ButtonParams ?? new List<ButtonMetadataDto>();
            bool templateHasDynamicUrl = templButtons.Any(b =>
                string.Equals(b.SubType ?? "url", "url", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(b.ParameterValue) &&
                b.ParameterValue!.Contains("{{"));

            if (templateHasDynamicUrl)
            {
                var hasCampaignUrlValues = (campaign.MultiButtons ?? new List<CampaignButton>())
                    .Any(cb => !string.IsNullOrWhiteSpace(cb.Value));
                if (!hasCampaignUrlValues)
                    resp.Notes.Add("Template defines dynamic URL button(s) with placeholders, but campaign has no URL button values configured.");
            }

            // Provider normalization for preview
            var provider = (campaign.Provider ?? "META_CLOUD").Trim().ToUpperInvariant();
            if (provider != "PINNACLE" && provider != "META_CLOUD")
            {
                resp.Notes.Add($"Invalid provider on campaign: '{campaign.Provider}'. Dry run will assume META_CLOUD.");
                provider = "META_CLOUD";
            }

            // Slice some recipients (prefer latest activity; CreatedAt is not on CampaignRecipient)
            var recipients = (campaign.Recipients ?? new List<CampaignRecipient>())
     .OrderByDescending(r => (DateTime?)r.UpdatedAt
                              ?? r.MaterializedAt
                              ?? r.SentAt
                              ?? DateTime.MinValue)
     .Take(Math.Clamp(maxRecipients, 1, 200))
     .ToList();

            resp.RecipientsConsidered = recipients.Count;

            // Helper: resolve a phone for a recipient
            static string? ResolveRecipientPhone(CampaignRecipient r) =>
                r?.Contact?.PhoneNumber ?? r?.AudienceMember?.PhoneE164 ?? r?.AudienceMember?.PhoneRaw;

            int okCount = 0, errCount = 0;

            foreach (var r in recipients)
            {
                var phoneResolved = ResolveRecipientPhone(r) ?? "";
                var contactName = r.Contact?.Name ?? r.AudienceMember?.Name;

                var one = new CampaignDryRunRecipientResultDto
                {
                    ContactId = r.ContactId,
                    ContactName = contactName,
                    PhoneNumber = phoneResolved
                };

                // Phone checks (presence + basic shape)
                var phone = (one.PhoneNumber ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(phone))
                {
                    one.Errors.Add("Recipient phone missing (no Contact and no AudienceMember phone).");
                }
                else if (!Regex.IsMatch(phone, @"^\+?\d{8,15}$"))
                {
                    one.Warnings.Add("Recipient phone may be invalid (basic format check failed).");
                }

                try
                {
                    // Always synthesize a contact to avoid null derefs in builders
                    var contactForTemplating = r.Contact ?? new Contact
                    {
                        Id = Guid.Empty,
                        BusinessId = campaign.BusinessId,
                        PhoneNumber = phoneResolved,
                        Name = contactName ?? "Customer"
                    };

                    // Buttons ordered like send path: by Position then original index; limit 3
                    var buttons = (campaign.MultiButtons ?? new List<CampaignButton>())
                        .Select((b, idx) => new { Btn = b, idx })
                        .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                        .ThenBy(x => x.idx)
                        .Select(x => x.Btn)
                        .Take(3)
                        .ToList();

                    // Build components for preview (match send path) — single call, discard out URLs
                    List<object> components;
                    var isImage = (campaign.CampaignType ?? "text")
                        .Equals("image", StringComparison.OrdinalIgnoreCase);

                    if (isImage)
                    {
                        components = (provider == "PINNACLE")
                            ? BuildImageTemplateComponents_Pinnacle(
                                campaign.ImageUrl, providedParams, buttons, templateMeta, Guid.NewGuid(), contactForTemplating, out _)
                            : BuildImageTemplateComponents_Meta(
                                campaign.ImageUrl, providedParams, buttons, templateMeta, Guid.NewGuid(), contactForTemplating, out _);
                    }
                    else
                    {
                        components = (provider == "PINNACLE")
                            ? BuildTextTemplateComponents_Pinnacle(
                                providedParams, buttons, templateMeta, Guid.NewGuid(), contactForTemplating, out _)
                            : BuildTextTemplateComponents_Meta(
                                providedParams, buttons, templateMeta, Guid.NewGuid(), contactForTemplating, out _);
                    }

                    // Additional validations like the send path: blank required params
                    if (templateMeta.PlaceholderCount > 0 &&
                        (providedParams.Count < templateMeta.PlaceholderCount ||
                         providedParams.Take(templateMeta.PlaceholderCount).Any(string.IsNullOrWhiteSpace)))
                    {
                        one.Errors.Add($"Missing body parameter(s): template requires {templateMeta.PlaceholderCount}, provided {providedParams.Count} (or some blank).");
                    }

                    one.ProviderComponents = components;
                    one.WouldSend = one.Errors.Count == 0;
                    if (one.WouldSend) okCount++; else errCount++;
                }
                catch (Exception ex)
                {
                    one.Errors.Add(ex.Message);
                    one.WouldSend = false;
                    errCount++;
                }

                resp.Results.Add(one);
            }

            resp.WouldSendCount = okCount;
            resp.ErrorCount = errCount;

            // Billability (best-effort)
            resp.EstimatedChargeable = true;
            resp.EstimatedConversationCategory = "template_outbound";
            if (!resp.Notes.Any(n => n.Contains("Template messages are typically chargeable")))
                resp.Notes.Add("Estimation: Template messages are typically chargeable and start a new conversation unless covered by free-entry flows.");

            return resp;
        }

        private static List<CampaignButtonDto> MapButtonVarsToButtonDtos(Dictionary<string, string>? vars)
        {
            var list = new List<CampaignButtonDto>();
            if (vars == null || vars.Count == 0) return list;

            // We only care about URL buttons 1..3; take the param text
            for (var i = 1; i <= 3; i++)
            {
                if (vars.TryGetValue($"button{i}.url_param", out var param) && !string.IsNullOrWhiteSpace(param))
                {
                    list.Add(new CampaignButtonDto
                    {
                        ButtonText = $"Button {i}",   // optional; purely cosmetic
                        ButtonType = "url",
                        TargetUrl = param
                    });
                }
            }
            return list;
        }
        private async Task<ResponseResult> SendDocumentTemplateCampaignAsync(Campaign campaign)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("[DocSend] Begin. campaignId={CampaignId}", campaign.Id);

            // force an IEnumerable → List and use a distinct name to avoid symbol collisions
            var recipientsList = (campaign.Recipients ?? Enumerable.Empty<CampaignRecipient>())
                    .Where(r =>
                    !string.IsNullOrWhiteSpace(r.Contact?.PhoneNumber) ||
                    !string.IsNullOrWhiteSpace(r.AudienceMember?.PhoneE164))
                         .ToList();

            // Use Any() (robust even if someone shadows Count somewhere)
            if (!recipientsList.Any())
                return ResponseResult.ErrorInfo("⚠️ No valid recipients with phone numbers.");

            var templateName = campaign.MessageTemplate;
            var languageCode = "en_US"; // keep consistent with your image/video path
            var provider = (campaign.Provider ?? "META").ToUpperInvariant();
            var phoneNumberId = campaign.PhoneNumberId;

            // optional static fallback (we don't have Campaign.DocumentUrl in this branch)
            var staticDocUrl = campaign.ImageUrl;

            var ok = 0; var fail = 0;

            foreach (var r in recipientsList)
            {
                var to = r.Contact?.PhoneNumber ?? r.AudienceMember?.PhoneE164 ?? "";
                if (string.IsNullOrWhiteSpace(to)) continue;

                try
                {
                    // These helpers were added earlier:
                    var templateParams = BuildBodyParametersForRecipient(campaign, r);
                    var buttonVars = BuildButtonParametersForRecipient(campaign, r);
                    var buttonsDto = MapButtonVarsToButtonDtos(buttonVars);
                    // Per-recipient doc header; no campaign-level DocumentUrl in this branch
                    var headerDocUrl = ResolvePerRecipientValue(r, "header.document_url") ?? staticDocUrl;

                    var dto = new DocumentTemplateMessageDto
                    {
                        BusinessId = campaign.BusinessId,
                        RecipientNumber = to,
                        TemplateName = templateName,
                        LanguageCode = languageCode,
                        HeaderDocumentUrl = headerDocUrl,
                        // match your DTO property names exactly — use the ones your MessageEngine expects:
                        Parameters = templateParams,   // or TemplateParameters if that's your DTO
                        Buttons = buttonsDto,      // or ButtonParameters if that's your DTO
                        Provider = provider,
                        PhoneNumberId = phoneNumberId,
                        CTAFlowConfigId = campaign.CTAFlowConfigId,
                        TemplateBody = campaign.MessageBody
                    };

                    var sent = await _messageEngineService.SendDocumentTemplateMessageAsync(dto, campaign.BusinessId);
                    var success = sent.Success;

                    if (success) ok++; else fail++;

                    await LogSendAsync(campaign, r, to, provider, success, headerDocUrl, "document");
                    _logger.LogInformation("[DocSend] to={To} success={Success}", to, success);
                }
                catch (Exception ex)
                {
                    fail++;
                    _logger.LogError(ex, "[DocSend] failed to={To}", to);
                    await LogSendAsync(campaign, r, to, provider, false, staticDocUrl, "document", ex.Message);
                }
            }

            sw.Stop();
            var msg = $"Document campaign finished. Success={ok}, Failed={fail}";
            _logger.LogInformation("[DocSend] Done. campaignId={CampaignId} {Msg}", campaign.Id, msg);

            return fail == 0 ? ResponseResult.SuccessInfo(msg) : ResponseResult.ErrorInfo(msg);
        }
        private Task LogSendAsync(
                    Campaign campaign,
                    CampaignRecipient recipient,
                    string to, string provider,
                    bool success, string? headerUrl,
                    string channel, string? error = null)
        {
            _logger.LogInformation(
                "[SendLog] campaignId={CampaignId} to={To} provider={Provider} channel={Channel} success={Success} headerUrl={HeaderUrl} error={Error}",
                campaign.Id, to, provider, channel, success, headerUrl, error);

            // If/when you have a CampaignSendLogs table, insert there instead.
            return Task.CompletedTask;
        }

        private static string[] ReadResolvedParams(CampaignRecipient r)
        {
            var s = r?.ResolvedParametersJson;
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            try
            {
                return JsonConvert.DeserializeObject<string[]>(s) ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static Dictionary<string, string> ReadResolvedButtonVars(CampaignRecipient r)
        {
            var s = r?.ResolvedButtonUrlsJson;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(s)) return dict;
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(s)
                       ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return dict;
            }
        }

        private static string? TryGetHeaderMedia(Dictionary<string, string> vars, params string[] keys)
        {
            foreach (var k in keys)
                if (!string.IsNullOrWhiteSpace(k) && vars.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }

        public Task<object> SendVideoTemplateMessageAsync(VideoTemplateMessageDto dto, Guid businessId)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> CheckNameAvailableAsync(Guid businessId, string name)
        {
            var exists = await _context.Campaigns
                .AsNoTracking()
                .AnyAsync(c => c.BusinessId == businessId && c.Name == name);
            return !exists;
        }

        public async Task RescheduleAsync(Guid businessId, Guid campaignId, DateTime newUtcTime)
        {
            var now = DateTime.UtcNow;
            if (newUtcTime <= now) throw new InvalidOperationException("New time must be in the future (UTC).");

            var c = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == campaignId && x.BusinessId == businessId);
            if (c == null) throw new KeyNotFoundException("Campaign not found.");

            c.ScheduledAt = newUtcTime;
            c.Status = "Queued"; // UI can show as “Scheduled”
            c.UpdatedAt = now;

            var job = await _context.OutboundCampaignJobs
                .FirstOrDefaultAsync(j => j.CampaignId == campaignId && j.Status == "queued");

            if (job != null)
            {
                job.NextAttemptAt = newUtcTime;
                job.UpdatedAt = now;
            }
            else
            {
                await _context.OutboundCampaignJobs.AddAsync(new OutboundCampaignJob
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    Status = "queued",
                    Attempt = 0,
                    MaxAttempts = 5,
                    NextAttemptAt = newUtcTime,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task EnqueueNowAsync(Guid businessId, Guid campaignId)
        {
            var now = DateTime.UtcNow;

            var c = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == campaignId && x.BusinessId == businessId);
            if (c == null) throw new KeyNotFoundException("Campaign not found.");

            var job = await _context.OutboundCampaignJobs
                .FirstOrDefaultAsync(j => j.CampaignId == campaignId && j.Status == "queued");

            if (job == null)
            {
                job = new OutboundCampaignJob
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    Status = "queued",
                    Attempt = 0,
                    MaxAttempts = 5,
                    CreatedAt = now,
                };
                await _context.OutboundCampaignJobs.AddAsync(job);
            }
            job.NextAttemptAt = now;
            job.UpdatedAt = now;

            c.ScheduledAt = null;
            c.Status = "Queued";
            c.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }

        public async Task CancelScheduleAsync(Guid businessId, Guid campaignId)
        {
            var now = DateTime.UtcNow;

            var c = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == campaignId && x.BusinessId == businessId);
            if (c == null) throw new KeyNotFoundException("Campaign not found.");

            var job = await _context.OutboundCampaignJobs
                .FirstOrDefaultAsync(j => j.CampaignId == campaignId && j.Status == "queued");

            if (job != null)
            {
                job.Status = "canceled";
                job.UpdatedAt = now;
            }

            c.ScheduledAt = null;
            c.Status = "Draft";
            c.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }
        public async Task<CampaignUsageDto?> GetCampaignUsageAsync(Guid businessId, Guid campaignId)
        {
            // Base campaign (scoped to business & not deleted)
            var baseRow = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.BusinessId == businessId && !c.IsDeleted)
                .Select(c => new
                {
                    c.Id,
                    c.BusinessId,
                    c.Name,
                    c.Status,
                    c.CreatedAt,
                    c.ScheduledAt,
                    c.CTAFlowConfigId
                })
                .FirstOrDefaultAsync();

            if (baseRow == null) return null;

            var statusNorm = (baseRow.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(statusNorm)) statusNorm = "DRAFT";

            // Counts
            var recipientsCount = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.CampaignId == campaignId)
                .CountAsync();

            var queuedJobsCount = await _context.OutboundCampaignJobs
                .AsNoTracking()
                .Where(j => j.CampaignId == campaignId)
                .CountAsync();

            var sendLogsCount = await _context.CampaignSendLogs
                .AsNoTracking()
                .Where(l => l.CampaignId == campaignId)
                .CountAsync();

            // Compute FirstSentAt from logs (since Campaign has no FirstSentAt column)
            var firstSentAt = await _context.CampaignSendLogs
                .AsNoTracking()
                .Where(l => l.CampaignId == campaignId)
                .OrderBy(l => l.SentAt ?? l.CreatedAt) // prefer SentAt if present, else CreatedAt
                .Select(l => (DateTime?)(l.SentAt ?? l.CreatedAt))
                .FirstOrDefaultAsync();

            return new CampaignUsageDto
            {
                CampaignId = baseRow.Id,
                Name = baseRow.Name,
                Status = statusNorm,                  // string, normalized
                Recipients = recipientsCount,             // int
                QueuedJobs = queuedJobsCount,             // int
                SendLogs = sendLogsCount,               // int
                HasFlow = baseRow.CTAFlowConfigId.HasValue,
                FlowId = baseRow.CTAFlowConfigId,
                CreatedAt = baseRow.CreatedAt,           // DateTime?
                ScheduledAt = baseRow.ScheduledAt,         // DateTime?
                FirstSentAt = firstSentAt                  // DateTime? (from logs)
            };
        }

        private async Task EnqueueOutboundJobsAsync(
           Campaign campaign,
           string provider,
           string mediaType,
           string templateName,
           string languageCode,
           string? phoneNumberId,
           IEnumerable<(CampaignRecipient r, string paramsJson, string btnsJson, string? headerUrl, string idemKey)> items,
           CancellationToken ct = default)
        {
            const int batchSize = 500;
            var buffer = new List<OutboundMessageJob>(batchSize);
            var now = DateTime.UtcNow;

            foreach (var it in items)
            {
                buffer.Add(new OutboundMessageJob
                {
                    Id = Guid.NewGuid(),
                    BusinessId = campaign.BusinessId,
                    CampaignId = campaign.Id,
                    RecipientId = it.r.Id,
                    Provider = (provider ?? string.Empty).Trim(),
                    MediaType = mediaType,
                    TemplateName = templateName,
                    LanguageCode = languageCode,
                    PhoneNumberId = phoneNumberId,
                    ResolvedParamsJson = it.paramsJson,
                    ResolvedButtonUrlsJson = it.btnsJson,
                    HeaderMediaUrl = it.headerUrl,
                    MessageBody = campaign.MessageBody,
                    // ✅ Allow repeats — do not use idempotency for campaign sends
                    IdempotencyKey = null,
                    Status = "Pending",
                    Attempt = 0,
                    CreatedAt = now,
                    NextAttemptAt = now,

                });

                if (buffer.Count >= batchSize)
                {
                    _context.OutboundMessageJobs.AddRange(buffer);
                    await _context.SaveChangesAsync(ct);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                _context.OutboundMessageJobs.AddRange(buffer);
                await _context.SaveChangesAsync(ct);
                buffer.Clear();
            }
        }



    }
    #endregion
}


