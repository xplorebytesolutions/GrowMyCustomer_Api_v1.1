using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Helpers;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    public class CTAFlowService : ICTAFlowService
    {
        private readonly AppDbContext _context;
        private readonly IMessageEngineService _messageEngineService;
        private readonly IWhatsAppTemplateFetcherService _templateFetcherService;

        public CTAFlowService(
            AppDbContext context,
            IMessageEngineService messageEngineService,
            IWhatsAppTemplateFetcherService templateFetcherService)
        {
            _context = context;
            _messageEngineService = messageEngineService;
            _templateFetcherService = templateFetcherService;
        }

        // ---------------------------
        // CREATE (draft-only, no edit)
        // ---------------------------
        public async Task<ResponseResult> SaveVisualFlowAsync(
            SaveVisualFlowDto dto,
            Guid businessId,
            string createdBy)
        {
            try
            {
                Log.Information("🧠 SaveVisualFlow (create-only) | FlowName: {FlowName} | Biz: {BusinessId}",
                    dto.FlowName, businessId);

                // 0) Validate
                if (dto.Nodes == null || !dto.Nodes.Any())
                    return ResponseResult.ErrorInfo("❌ Cannot save an empty flow. Please add at least one step.");

                var trimmedName = (dto.FlowName ?? "").Trim();
                if (trimmedName.Length == 0)
                    return ResponseResult.ErrorInfo("❌ Flow name is required.");

                // 1) Enforce unique active name per business (create-only)
                var nameExists = await _context.CTAFlowConfigs
                    .AnyAsync(f => f.BusinessId == businessId && f.FlowName == trimmedName && f.IsActive);
                if (nameExists)
                {
                    Log.Warning("⚠️ Duplicate flow name '{Name}' for business {Biz}.", trimmedName, businessId);
                    return ResponseResult.ErrorInfo("❌ A flow with this name already exists.");
                }

                await using var tx = await _context.Database.BeginTransactionAsync();

                // 2) Insert FlowConfig AS DRAFT (force IsPublished=false)
                var flow = new CTAFlowConfig
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    FlowName = trimmedName,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    IsPublished = false // << always draft on create
                };
                _context.CTAFlowConfigs.Add(flow);

                // 3) Steps (map incoming node ids so we can wire links)
                var stepMap = new Dictionary<string, CTAFlowStep>(StringComparer.OrdinalIgnoreCase);
                var orderIndex = 0;

                foreach (var node in dto.Nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.Id)) continue;

                    var step = new CTAFlowStep
                    {
                        Id = Guid.NewGuid(),
                        CTAFlowConfigId = flow.Id,
                        StepOrder = orderIndex++,
                        TemplateToSend = node.TemplateName,
                        TemplateType = node.TemplateType ?? "UNKNOWN",
                        TriggerButtonText = node.TriggerButtonText ?? "",
                        TriggerButtonType = node.TriggerButtonType ?? "cta",
                        PositionX = node.PositionX == 0 ? Random.Shared.Next(100, 600) : node.PositionX,
                        PositionY = node.PositionY == 0 ? Random.Shared.Next(100, 400) : node.PositionY,
                        UseProfileName = node.UseProfileName,
                        ProfileNameSlot = node.ProfileNameSlot,
                        ButtonLinks = new List<FlowButtonLink>()
                    };

                    // Only text templates may use profile name slot
                    var isTextTemplate = string.Equals(step.TemplateType, "text_template", StringComparison.OrdinalIgnoreCase);
                    if (!isTextTemplate)
                    {
                        step.UseProfileName = false;
                        step.ProfileNameSlot = null;
                    }
                    else if (!step.UseProfileName)
                    {
                        step.ProfileNameSlot = null;
                    }
                    else if (!step.ProfileNameSlot.HasValue || step.ProfileNameSlot.Value < 1)
                    {
                        step.ProfileNameSlot = 1;
                    }

                    stepMap[node.Id] = step;
                    _context.CTAFlowSteps.Add(step);
                }

                // 4) Wire links per node via edges (SourceHandle == button text)
                var edges = dto.Edges ?? new List<FlowEdgeDto>();

                foreach (var node in dto.Nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.Id) || !stepMap.TryGetValue(node.Id, out var fromStep))
                        continue;

                    var outEdges = edges
                        .Where(e => string.Equals(e.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var orderedButtons = (node.Buttons ?? new List<LinkButtonDto>())
                        .OrderBy(b => b.Index < 0 ? int.MaxValue : b.Index)
                        .ThenBy(b => b.Text ?? string.Empty)
                        .ToList();

                    short nextIdx = 0;

                    foreach (var btn in orderedButtons)
                    {
                        var text = (btn.Text ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(text)) continue;
                        if (!seenTexts.Add(text)) continue; // dedupe

                        var edge = outEdges.FirstOrDefault(e =>
                            string.Equals(e.SourceHandle ?? string.Empty, text, StringComparison.OrdinalIgnoreCase));
                        if (edge == null) continue;

                        if (!stepMap.TryGetValue(edge.ToNodeId, out var toStep)) continue;

                        var finalIndex = btn.Index >= 0 ? btn.Index : nextIdx;
                        nextIdx = (short)(finalIndex + 1);

                        var link = new FlowButtonLink
                        {
                            Id = Guid.NewGuid(),
                            CTAFlowStepId = fromStep.Id,
                            NextStepId = toStep.Id,
                            ButtonText = text,
                            ButtonType = string.IsNullOrWhiteSpace(btn.Type) ? "QUICK_REPLY" : btn.Type,
                            ButtonSubType = btn.SubType ?? string.Empty,
                            ButtonValue = btn.Value ?? string.Empty,
                            ButtonIndex = (short)finalIndex
                        };

                        _context.FlowButtonLinks.Add(link);
                        fromStep.ButtonLinks.Add(link);

                        // convenience: populate target's trigger info
                        toStep.TriggerButtonText = text;
                        toStep.TriggerButtonType = (btn.Type ?? "QUICK_REPLY").ToLowerInvariant();
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                Log.Information("✅ Flow created '{Flow}' | Steps: {Steps} | Links: {Links}",
                    flow.FlowName, stepMap.Count, stepMap.Values.Sum(s => s.ButtonLinks.Count));

                return ResponseResult.SuccessInfo("✅ Flow created.", new { flowId = flow.Id });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception while saving (create) flow");
                return ResponseResult.ErrorInfo("❌ Internal error while saving flow.");
            }
        }

        // ---------------------------
        // LISTS
        // ---------------------------
        public async Task<List<VisualFlowSummaryDto>> GetAllPublishedFlowsAsync(Guid businessId)
        {
            return await _context.CTAFlowConfigs
                .Where(f => f.BusinessId == businessId && f.IsPublished)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new VisualFlowSummaryDto
                {
                    Id = f.Id,
                    FlowName = f.FlowName,
                    IsPublished = f.IsPublished,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<VisualFlowSummaryDto>> GetAllDraftFlowsAsync(Guid businessId)
        {
            return await _context.CTAFlowConfigs
                .Where(f => f.BusinessId == businessId && !f.IsPublished && f.IsActive)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new VisualFlowSummaryDto
                {
                    Id = f.Id,
                    FlowName = f.FlowName,
                    CreatedAt = f.CreatedAt,
                    IsPublished = f.IsPublished
                })
                .ToListAsync();
        }

        // ---------------------------
        // DETAIL LOADERS
        // ---------------------------
        public async Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid flowId, Guid businessId)
        {
            var flow = await _context.CTAFlowConfigs
                .Include(c => c.Steps)
                    .ThenInclude(s => s.ButtonLinks)
                .FirstOrDefaultAsync(c => c.Id == flowId && c.BusinessId == businessId && c.IsActive);

            if (flow == null) return null;

            // Prefetch template metadata
            var templateMap = new Dictionary<string, TemplateMetadataDto>(StringComparer.OrdinalIgnoreCase);
            var uniqueNames = flow.Steps
                .Select(s => s.TemplateToSend)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var name in uniqueNames)
            {
                try
                {
                    var tpl = await _templateFetcherService.GetTemplateByNameAsync(
                        businessId, name!, includeButtons: true);
                    if (tpl != null) templateMap[name!] = tpl;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠️ Failed to fetch template metadata for {Template}", name);
                }
            }

            var nodes = flow.Steps.Select(step =>
            {
                templateMap.TryGetValue(step.TemplateToSend ?? "", out var tpl);

                var dbLinks = step.ButtonLinks ?? new List<FlowButtonLink>();
                var dbButtons = dbLinks
                    .OrderBy(b => b.ButtonIndex)
                    .Select(link => new LinkButtonDto
                    {
                        Text = link.ButtonText,
                        Type = link.ButtonType,
                        SubType = link.ButtonSubType,
                        Value = link.ButtonValue,
                        Index = link.ButtonIndex,
                        TargetNodeId = link.NextStepId?.ToString()
                    });

                var templateButtons = (tpl?.ButtonParams ?? new List<ButtonMetadataDto>())
                    .Where(btn => !dbLinks.Any(bl => string.Equals(bl.ButtonText, btn.Text, StringComparison.OrdinalIgnoreCase)))
                    .Select(btn => new LinkButtonDto { Text = btn.Text });

                return new FlowNodeDto
                {
                    Id = step.Id.ToString(),
                    TemplateName = step.TemplateToSend,
                    TemplateType = step.TemplateType,
                    MessageBody = string.IsNullOrWhiteSpace(tpl?.Body) ? "— no body found —" : tpl!.Body,
                    TriggerButtonText = step.TriggerButtonText,
                    TriggerButtonType = step.TriggerButtonType,
                    PositionX = step.PositionX ?? 100,
                    PositionY = step.PositionY ?? 100,
                    RequiredTag = step.RequiredTag,
                    RequiredSource = step.RequiredSource,
                    UseProfileName = step.UseProfileName,
                    ProfileNameSlot = step.ProfileNameSlot,
                    Buttons = dbButtons.Concat(templateButtons).ToList()
                };
            }).ToList();

            var edges = flow.Steps
                .SelectMany(step =>
                    (step.ButtonLinks ?? Enumerable.Empty<FlowButtonLink>())
                    .Where(l => l.NextStepId.HasValue)
                    .Select(l => new FlowEdgeDto
                    {
                        FromNodeId = step.Id.ToString(),
                        ToNodeId = l.NextStepId!.Value.ToString(),
                        SourceHandle = l.ButtonText
                    }))
                .ToList();

            return new SaveVisualFlowDto
            {
                FlowName = flow.FlowName,
                IsPublished = flow.IsPublished,
                Nodes = nodes,
                Edges = edges
            };
        }

        public async Task<ResponseResult> GetVisualFlowAsync(Guid flowId, Guid businessId)
        {
            try
            {
                var flow = await _context.CTAFlowConfigs
                    .AsNoTracking()
                    .Where(f => f.IsActive && f.BusinessId == businessId && f.Id == flowId)
                    .Select(f => new
                    {
                        f.Id,
                        f.FlowName,
                        f.IsPublished,
                        Steps = _context.CTAFlowSteps
                            .Where(s => s.CTAFlowConfigId == f.Id)
                            .OrderBy(s => s.StepOrder)
                            .Select(s => new
                            {
                                s.Id,
                                s.StepOrder,
                                s.TemplateToSend,
                                s.TemplateType,
                                s.TriggerButtonText,
                                s.TriggerButtonType,
                                s.PositionX,
                                s.PositionY,
                                s.UseProfileName,
                                s.ProfileNameSlot,
                                Buttons = _context.FlowButtonLinks
                                    .Where(b => b.CTAFlowStepId == s.Id)
                                    .OrderBy(b => b.ButtonIndex)
                                    .Select(b => new
                                    {
                                        b.ButtonText,
                                        b.ButtonType,
                                        b.ButtonSubType,
                                        b.ButtonValue,
                                        b.ButtonIndex,
                                        b.NextStepId
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (flow == null)
                    return ResponseResult.ErrorInfo("Flow not found.");

                var nodes = flow.Steps.Select(s => new
                {
                    id = s.Id.ToString(),
                    positionX = s.PositionX ?? 0,
                    positionY = s.PositionY ?? 0,
                    templateName = s.TemplateToSend,
                    templateType = s.TemplateType,
                    triggerButtonText = s.TriggerButtonText ?? string.Empty,
                    triggerButtonType = s.TriggerButtonType ?? "cta",
                    requiredTag = string.Empty,
                    requiredSource = string.Empty,
                    useProfileName = s.UseProfileName,
                    profileNameSlot = (s.ProfileNameSlot.HasValue && s.ProfileNameSlot.Value > 0) ? s.ProfileNameSlot.Value : 1,
                    buttons = s.Buttons.Select(b => new
                    {
                        text = b.ButtonText,
                        type = b.ButtonType,
                        subType = b.ButtonSubType,
                        value = b.ButtonValue,
                        targetNodeId = b.NextStepId == Guid.Empty ? null : b.NextStepId.ToString(),
                        index = (int)(b.ButtonIndex)
                    })
                });

                var edges = flow.Steps
                    .SelectMany(s => s.Buttons
                        .Where(b => b.NextStepId != Guid.Empty)
                        .Select(b => new
                        {
                            fromNodeId = s.Id.ToString(),
                            toNodeId = b.NextStepId.ToString(),
                            sourceHandle = b.ButtonText
                        }));

                var payload = new
                {
                    flowName = flow.FlowName,
                    isPublished = flow.IsPublished,
                    nodes,
                    edges
                };

                return ResponseResult.SuccessInfo("Flow loaded.", payload);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception while loading visual flow {FlowId}", flowId);
                return ResponseResult.ErrorInfo("Internal error while loading flow.");
            }
        }

        // ---------------------------
        // DELETE (only if not attached)
        // ---------------------------
        public async Task<ResponseResult> DeleteFlowAsync(Guid flowId, Guid businessId, string deletedBy)
        {
            var flow = await _context.CTAFlowConfigs
                .Include(f => f.Steps)
                    .ThenInclude(s => s.ButtonLinks)
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

            if (flow == null)
                return ResponseResult.ErrorInfo("❌ Flow not found or does not belong to you.");

            var inUseQuery = _context.Campaigns
                .Where(c => c.BusinessId == businessId &&
                            !c.IsDeleted &&
                            c.CTAFlowConfigId == flowId);

            var inUseCount = await inUseQuery.CountAsync();
            if (inUseCount > 0)
            {
                Log.Warning("❌ Delete flow blocked. Flow {FlowId} is used by {Count} campaigns.", flowId, inUseCount);
                // Keep message; controller will fetch campaigns for modal
                return ResponseResult.ErrorInfo(
                    $"❌ Cannot delete. This flow is attached to {inUseCount} campaign(s). Delete those campaigns first.");
            }

            foreach (var step in flow.Steps)
                _context.FlowButtonLinks.RemoveRange(step.ButtonLinks);

            _context.CTAFlowSteps.RemoveRange(flow.Steps);
            _context.CTAFlowConfigs.Remove(flow);

            await _context.SaveChangesAsync();
            return ResponseResult.SuccessInfo("✅ Flow deleted.");
        }

        public async Task<IReadOnlyList<AttachedCampaignDto>> GetAttachedCampaignsAsync(Guid flowId, Guid businessId)
        {
            var q = _context.Campaigns
                .Where(c => c.BusinessId == businessId && !c.IsDeleted && c.CTAFlowConfigId == flowId);

            var firstSends = await _context.CampaignSendLogs
                .Where(s => s.BusinessId == businessId && s.CampaignId != Guid.Empty)
                .GroupBy(s => s.CampaignId)
                .Select(g => new { CampaignId = g.Key, FirstSentAt = (DateTime?)g.Min(s => s.CreatedAt) })
                .ToListAsync();

            var firstSendMap = firstSends.ToDictionary(x => x.CampaignId, x => x.FirstSentAt);

            var list = await q
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Status,
                    c.ScheduledAt,
                    c.CreatedAt,
                    c.CreatedBy
                })
                .ToListAsync();

            return list.Select(x => new AttachedCampaignDto(
                x.Id,
                x.Name,
                x.Status,
                x.ScheduledAt,
                x.CreatedAt,
                x.CreatedBy,
                firstSendMap.TryGetValue(x.Id, out var ts) ? ts : null
            )).ToList();
        }

        public async Task<bool> HardDeleteFlowIfUnusedAsync(Guid flowId, Guid businessId)
        {
            var flow = await _context.CTAFlowConfigs
                .Include(f => f.Steps)
                    .ThenInclude(s => s.ButtonLinks)
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

            if (flow is null) return false;

            var attached = await _context.Campaigns
                .Where(c => c.BusinessId == businessId && !c.IsDeleted && c.CTAFlowConfigId == flowId)
                .AnyAsync();
            if (attached) return false;

            foreach (var step in flow.Steps)
                _context.FlowButtonLinks.RemoveRange(step.ButtonLinks);
            _context.CTAFlowSteps.RemoveRange(flow.Steps);
            _context.CTAFlowConfigs.Remove(flow);

            await _context.SaveChangesAsync();
            return true;
        }

        // ---------------------------
        // PUBLISH (by id, flip flag)
        // ---------------------------
        public async Task<bool> PublishFlowAsync(Guid flowId, Guid businessId, string user)
        {
            var flow = await _context.CTAFlowConfigs
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

            if (flow is null) return false;

            flow.IsPublished = true;
            flow.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------------------------
        // RUNTIME / Matching / Execute
        // ---------------------------
        public async Task<CTAFlowStep?> MatchStepByButtonAsync(
            Guid businessId,
            string buttonText,
            string buttonType,
            string templateName,
            Guid? campaignId = null)
        {
            var normalizedButtonText = buttonText?.Trim().ToLower() ?? "";
            var normalizedButtonType = buttonType?.Trim().ToLower() ?? "";
            var normalizedTemplateName = templateName?.Trim().ToLower() ?? "";

            if (campaignId.HasValue)
            {
                var overrideStep = await _context.CampaignFlowOverrides
                    .Where(o =>
                        o.CampaignId == campaignId &&
                        o.ButtonText.ToLower() == normalizedButtonText &&
                        o.TemplateName.ToLower() == normalizedTemplateName)
                    .FirstOrDefaultAsync();

                if (overrideStep != null)
                {
                    var overrideTemplate = overrideStep.OverrideNextTemplate?.ToLower();
                    var matched = await _context.CTAFlowSteps
                        .Include(s => s.Flow)
                        .FirstOrDefaultAsync(s => s.TemplateToSend.ToLower() == overrideTemplate);
                    if (matched != null) return matched;
                }
            }

            var fallbackStep = await _context.CTAFlowSteps
                .Include(s => s.Flow)
                .Where(s =>
                    s.Flow.BusinessId == businessId &&
                    s.Flow.IsActive &&
                    s.Flow.IsPublished &&
                    s.TriggerButtonText.ToLower() == normalizedButtonText &&
                    s.TriggerButtonType.ToLower() == normalizedButtonType)
                .FirstOrDefaultAsync();

            return fallbackStep;
        }

        public async Task<ResponseResult> ExecuteVisualFlowAsync(Guid businessId, Guid startStepId, Guid trackingLogId, Guid? campaignSendLogId)
        {
            try
            {
                var log = await _context.TrackingLogs
                    .Include(l => l.Contact)
                        .ThenInclude(c => c.ContactTags)
                            .ThenInclude(ct => ct.Tag)
                    .FirstOrDefaultAsync(l => l.Id == trackingLogId);

                if (log == null) return ResponseResult.ErrorInfo("Tracking log not found.");

                var step = await GetChainedStepAsync(businessId, startStepId, log, log?.Contact);
                if (step == null) return ResponseResult.ErrorInfo("Step conditions not satisfied.");

                var args = new List<string>();
                if (step.UseProfileName && step.ProfileNameSlot is int slot && slot >= 1)
                {
                    var contact = log.Contact ?? await _context.Contacts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == (log.ContactPhone ?? ""));
                    var greet = (contact?.ProfileName ?? contact?.Name)?.Trim();
                    if (string.IsNullOrEmpty(greet)) greet = "there";
                    while (args.Count < slot) args.Add(string.Empty);
                    args[slot - 1] = greet;
                }

                ResponseResult sendResult;
                switch (step.TemplateType?.ToLower())
                {
                    case "image_template":
                        var imageDto = new ImageTemplateMessageDto
                        {
                            BusinessId = businessId,
                            RecipientNumber = log.ContactPhone ?? "",
                            TemplateName = step.TemplateToSend,
                            LanguageCode = "en_US"
                        };
                        sendResult = await _messageEngineService.SendImageTemplateMessageAsync(imageDto, businessId);
                        break;
                    case "text_template":
                    default:
                        var textDto = new SimpleTemplateMessageDto
                        {
                            RecipientNumber = log.ContactPhone ?? "",
                            TemplateName = step.TemplateToSend,
                            TemplateParameters = args
                        };
                        sendResult = await _messageEngineService.SendTemplateMessageSimpleAsync(businessId, textDto);
                        break;
                }

                var executionLog = new FlowExecutionLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    StepId = step.Id,
                    FlowId = step.CTAFlowConfigId,
                    Origin = FlowExecutionOrigin.Campaign, // added fro autoreply flow
                    CampaignSendLogId = campaignSendLogId,
                    TrackingLogId = trackingLogId,
                    ContactPhone = log.ContactPhone,
                    TriggeredByButton = step.TriggerButtonText,
                    TemplateName = step.TemplateToSend,
                    TemplateType = step.TemplateType,
                    Success = sendResult.Success,
                    ErrorMessage = sendResult.ErrorMessage,
                    RawResponse = sendResult.RawResponse,
                    ExecutedAt = DateTime.UtcNow
                };

                _context.FlowExecutionLogs.Add(executionLog);
                await _context.SaveChangesAsync();

                return ResponseResult.SuccessInfo($"Flow step executed. Sent: {sendResult.Success}", null, sendResult.RawResponse);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception during ExecuteVisualFlowAsync()");
                return ResponseResult.ErrorInfo("Internal error during flow execution.");
            }
        }

        public Task<CTAFlowStep?> GetChainedStepAsync(Guid businessId, Guid? nextStepId)
            => GetChainedStepAsync(businessId, nextStepId, null, null);

        public async Task<CTAFlowStep?> GetChainedStepAsync(Guid businessId, Guid? nextStepId, TrackingLog? trackingLog, Contact? contact)
        {
            if (nextStepId == null) return null;

            var flow = await _context.CTAFlowConfigs
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(f =>
                    f.BusinessId == businessId &&
                    f.Steps.Any(s => s.Id == nextStepId));

            var followUpStep = flow?.Steps.FirstOrDefault(s => s.Id == nextStepId);
            if (followUpStep == null) return null;

            if (trackingLog != null)
            {
                var isMatch = StepMatchingHelper.IsStepMatched(followUpStep, trackingLog, contact);
                if (!isMatch) return null;
            }

            return followUpStep;
        }

        public async Task<CTAFlowStep?> GetChainedStepWithContextAsync(Guid businessId, Guid? nextStepId, Guid? trackingLogId)
        {
            var log = await _context.TrackingLogs
                .Include(l => l.Contact)
                    .ThenInclude(c => c.ContactTags)
                        .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(l => l.Id == trackingLogId);

            return await GetChainedStepAsync(businessId, nextStepId, log, log?.Contact);
        }

        // ✅ MISSING IMPLEMENTATION (to satisfy the interface)
        public async Task<FlowButtonLink?> GetLinkAsync(Guid flowId, Guid sourceStepId, short buttonIndex)
        {
            return await _context.FlowButtonLinks
                .Where(l => l.CTAFlowStepId == sourceStepId
                            && l.NextStepId != null
                            && l.Step.CTAFlowConfigId == flowId
                            && l.ButtonIndex == buttonIndex)
                .SingleOrDefaultAsync();
        }
    }
}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Serilog;
//using xbytechat.api.AuthModule.Models;
//using xbytechat.api.CRM.Models;
//using xbytechat.api.Features.CTAFlowBuilder.DTOs;
//using xbytechat.api.Features.CTAFlowBuilder.Models;
//using xbytechat.api.Features.MessagesEngine.DTOs;
//using xbytechat.api.Features.MessagesEngine.Services;
//using xbytechat.api.Features.Tracking.Models;
//using xbytechat.api.Helpers;
//using xbytechat.api.WhatsAppSettings.DTOs;
//using xbytechat_api.WhatsAppSettings.Services;

//namespace xbytechat.api.Features.CTAFlowBuilder.Services
//{
//    public class CTAFlowService : ICTAFlowService
//    {
//        private readonly AppDbContext _context;
//        private readonly IMessageEngineService _messageEngineService;
//        private readonly IWhatsAppTemplateFetcherService _templateFetcherService;

//        public CTAFlowService(AppDbContext context, IMessageEngineService messageEngineService,
//            IWhatsAppTemplateFetcherService templateFetcherService
//            )
//        {
//            _context = context;
//            _messageEngineService = messageEngineService;
//            _templateFetcherService = templateFetcherService;
//        }

//        public async Task<Guid> CreateFlowWithStepsAsync(CreateFlowDto dto, Guid businessId, string createdBy)
//        {
//            var flow = new CTAFlowConfig
//            {
//                Id = Guid.NewGuid(),
//                FlowName = dto.FlowName,
//                BusinessId = businessId,
//                CreatedAt = DateTime.UtcNow,
//                CreatedBy = createdBy,
//                IsPublished = dto.IsPublished
//            };

//            foreach (var stepDto in dto.Steps)
//            {
//                var step = new CTAFlowStep
//                {
//                    Id = Guid.NewGuid(),
//                    CTAFlowConfigId = flow.Id,
//                    TriggerButtonText = stepDto.TriggerButtonText,
//                    TriggerButtonType = stepDto.TriggerButtonType,
//                    TemplateToSend = stepDto.TemplateToSend,
//                    StepOrder = stepDto.StepOrder,
//                    ButtonLinks = stepDto.ButtonLinks?.Select(link => new FlowButtonLink
//                    {
//                        ButtonText = link.ButtonText,
//                        NextStepId = link.NextStepId
//                    }).ToList() ?? new List<FlowButtonLink>()
//                };

//                flow.Steps.Add(step);
//            }

//            _context.CTAFlowConfigs.Add(flow);
//            await _context.SaveChangesAsync();

//            return flow.Id;
//        }

//        public async Task<CTAFlowConfig?> GetFlowByBusinessAsync(Guid businessId)
//        {
//            return await _context.CTAFlowConfigs
//                .Include(f => f.Steps.OrderBy(s => s.StepOrder))
//                .Where(f => f.BusinessId == businessId && f.IsActive && f.IsPublished)
//                .FirstOrDefaultAsync();
//        }

//        public async Task<CTAFlowConfig?> GetDraftFlowByBusinessAsync(Guid businessId)
//        {
//            return await _context.CTAFlowConfigs
//                .Include(f => f.Steps)
//                    .ThenInclude(s => s.ButtonLinks)
//                .Where(f => f.BusinessId == businessId && f.IsPublished == false)
//                .OrderByDescending(f => f.CreatedAt)
//                .FirstOrDefaultAsync();
//        }



//        public async Task<List<CTAFlowStep>> GetStepsForFlowAsync(Guid flowId)
//        {
//            return await _context.CTAFlowSteps
//                .Where(s => s.CTAFlowConfigId == flowId)
//                .OrderBy(s => s.StepOrder)
//                .ToListAsync();
//        }

//        public async Task<CTAFlowStep?> MatchStepByButtonAsync(
//            Guid businessId,
//            string buttonText,
//            string buttonType,
//            string TemplateName,
//            Guid? campaignId = null)
//        {
//            var normalizedButtonText = buttonText?.Trim().ToLower() ?? "";
//            var normalizedButtonType = buttonType?.Trim().ToLower() ?? "";
//            var normalizedTemplateName = TemplateName?.Trim().ToLower() ?? "";

//            // 1️⃣ Try campaign-specific override
//            if (campaignId.HasValue)
//            {
//                var overrideStep = await _context.CampaignFlowOverrides
//                    .Where(o =>
//                        o.CampaignId == campaignId &&
//                        o.ButtonText.ToLower() == normalizedButtonText &&
//                        o.TemplateName.ToLower() == normalizedTemplateName)
//                    .FirstOrDefaultAsync();

//                if (overrideStep != null)
//                {
//                    var overrideTemplate = overrideStep.OverrideNextTemplate?.ToLower();

//                    var matched = await _context.CTAFlowSteps
//                        .Include(s => s.Flow)
//                        .FirstOrDefaultAsync(s => s.TemplateToSend.ToLower() == overrideTemplate);

//                    if (matched != null)
//                    {
//                        Log.Information("🔁 Override matched: Template '{Template}' → Step '{StepId}'", overrideStep.OverrideNextTemplate, matched.Id);
//                        return matched;
//                    }

//                    Log.Warning("⚠️ Override found for button '{Button}' but no matching step for template '{Template}'", normalizedButtonText, overrideStep.OverrideNextTemplate);
//                }

//                else
//                {
//                    Log.Information("🟡 No campaign override found for button '{Button}' on template '{Template}'", normalizedButtonText, normalizedTemplateName);
//                }
//            }

//            // 2️⃣ Fallback to standard flow logic
//            var fallbackStep = await _context.CTAFlowSteps
//                .Include(s => s.Flow)
//                .Where(s =>
//                    s.Flow.BusinessId == businessId &&
//                    s.Flow.IsActive &&
//                    s.Flow.IsPublished &&
//                    s.TriggerButtonText.ToLower() == normalizedButtonText &&
//                    s.TriggerButtonType.ToLower() == normalizedButtonType)
//                .FirstOrDefaultAsync();

//            if (fallbackStep != null)
//            {
//                Log.Information("✅ Fallback flow step matched: StepId = {StepId}, Flow = {FlowName}", fallbackStep.Id, fallbackStep.Flow?.FlowName);
//            }
//            else
//            {
//                Log.Warning("❌ No fallback step matched for button '{ButtonText}' of type '{ButtonType}' in BusinessId: {BusinessId}", normalizedButtonText, normalizedButtonType, businessId);
//            }

//            return fallbackStep;
//        }


//        public async Task<bool> PublishFlowAsync(Guid flowId, Guid businessId, string user)
//        {
//            var flow = await _context.CTAFlowConfigs
//                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId && f.IsActive);

//            if (flow is null) return false;

//            // Optional validation: ensure it has at least 1 step, etc.
//            flow.IsPublished = true;
//            flow.UpdatedAt = DateTime.UtcNow;
//            await _context.SaveChangesAsync();
//            return true;
//        }

//        //public async Task<ResponseResult> PublishFlowAsync(Guid businessId, List<FlowStepDto> steps, string createdBy)
//        //{
//        //    try
//        //    {
//        //        // 🔥 1. Remove existing published flow for this business
//        //        var existingFlows = await _context.CTAFlowConfigs
//        //            .Where(f => f.BusinessId == businessId && f.IsPublished)
//        //            .ToListAsync();

//        //        if (existingFlows.Any())
//        //        {
//        //            _context.CTAFlowConfigs.RemoveRange(existingFlows);
//        //        }

//        //        // 🌱 2. Create new flow config
//        //        var flowConfig = new CTAFlowConfig
//        //        {
//        //            Id = Guid.NewGuid(),
//        //            BusinessId = businessId,
//        //            FlowName = "Published Flow - " + DateTime.UtcNow.ToString("yyyyMMdd-HHmm"),
//        //            IsPublished = true,
//        //            IsActive = true,
//        //            CreatedBy = createdBy,
//        //            CreatedAt = DateTime.UtcNow,
//        //            Steps = new List<CTAFlowStep>()
//        //        };

//        //        // 🔁 3. Convert each step DTO to model
//        //        foreach (var stepDto in steps)
//        //        {
//        //            var step = new CTAFlowStep
//        //            {
//        //                Id = Guid.NewGuid(),
//        //                CTAFlowConfigId = flowConfig.Id,
//        //                TriggerButtonText = stepDto.TriggerButtonText,
//        //                TriggerButtonType = stepDto.TriggerButtonType,
//        //                TemplateToSend = stepDto.TemplateToSend,
//        //                StepOrder = stepDto.StepOrder,
//        //                ButtonLinks = stepDto.ButtonLinks.Select(bl => new FlowButtonLink
//        //                {
//        //                    Id = Guid.NewGuid(),
//        //                    ButtonText = bl.ButtonText,
//        //                    NextStepId = bl.NextStepId,
//        //                }).ToList()
//        //            };

//        //            flowConfig.Steps.Add(step);
//        //        }

//        //        // 💾 4. Save to DB
//        //        await _context.CTAFlowConfigs.AddAsync(flowConfig);
//        //        await _context.SaveChangesAsync();

//        //        return ResponseResult.SuccessInfo("✅ Flow published successfully.");
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        Log.Error(ex, "❌ Error while publishing CTA flow.");
//        //        return ResponseResult.ErrorInfo("❌ Could not publish flow.");
//        //    }
//        //}


//        private static int CountBodyPlaceholders(string? body)
//        {
//            if (string.IsNullOrWhiteSpace(body)) return 0;
//            // counts {{1}}, {{2}}, ... ; ignores any non-numeric moustaches
//            var m = System.Text.RegularExpressions.Regex.Matches(body, @"\{\{\s*\d+\s*\}\}");
//            return m.Count;
//        }
//        public async Task<ResponseResult> SaveVisualFlowAsync(
//    SaveVisualFlowDto dto,
//    Guid businessId,
//    string createdBy)
//        {
//            try
//            {
//                Log.Information("🧠 SaveVisualFlow (create-only) | FlowName: {FlowName} | Biz: {BusinessId}",
//                    dto.FlowName, businessId);

//                // 0) Basic validation
//                if (dto.Nodes == null || !dto.Nodes.Any())
//                    return ResponseResult.ErrorInfo("❌ Cannot save an empty flow. Please add at least one step.");

//                var trimmedName = (dto.FlowName ?? "").Trim();
//                if (trimmedName.Length == 0)
//                    return ResponseResult.ErrorInfo("❌ Flow name is required.");

//                // 1) CREATE-ONLY: refuse duplicate name for this business
//                var nameExists = await _context.CTAFlowConfigs
//                    .AnyAsync(f => f.BusinessId == businessId && f.FlowName == trimmedName && f.IsActive);

//                if (nameExists)
//                {
//                    // IMPORTANT: this method is only for *new* flows.
//                    // If the user is editing an existing flow, the UI should call PUT /cta-flow/{id}.
//                    Log.Warning("⚠️ Duplicate flow name '{Name}' for business {Biz}.", trimmedName, businessId);
//                    return ResponseResult.ErrorInfo(
//                        "❌ A flow with this name already exists. Open that flow and edit it, or choose a different name.");
//                }

//                await using var tx = await _context.Database.BeginTransactionAsync();

//                // 2) Insert FlowConfig
//                var flow = new CTAFlowConfig
//                {
//                    Id = Guid.NewGuid(),
//                    BusinessId = businessId,
//                    FlowName = trimmedName,
//                    CreatedBy = createdBy,
//                    CreatedAt = DateTime.UtcNow,
//                    UpdatedAt = DateTime.UtcNow,
//                    IsActive = true,
//                    // You *can* allow creating as published, but most teams prefer create-as-draft:
//                    IsPublished = dto.IsPublished
//                };
//                _context.CTAFlowConfigs.Add(flow);

//                // 3) Build Steps
//                var stepMap = new Dictionary<string, CTAFlowStep>(StringComparer.OrdinalIgnoreCase);
//                var orderIndex = 0;

//                foreach (var node in dto.Nodes)
//                {
//                    if (string.IsNullOrWhiteSpace(node.Id)) continue;

//                    var step = new CTAFlowStep
//                    {
//                        Id = Guid.NewGuid(),
//                        CTAFlowConfigId = flow.Id,
//                        StepOrder = orderIndex++,
//                        TemplateToSend = node.TemplateName,
//                        TemplateType = node.TemplateType ?? "UNKNOWN",
//                        TriggerButtonText = node.TriggerButtonText ?? "",
//                        TriggerButtonType = node.TriggerButtonType ?? "cta",
//                        PositionX = node.PositionX == 0 ? Random.Shared.Next(100, 600) : node.PositionX,
//                        PositionY = node.PositionY == 0 ? Random.Shared.Next(100, 400) : node.PositionY,
//                        UseProfileName = node.UseProfileName,
//                        ProfileNameSlot = node.ProfileNameSlot,
//                        ButtonLinks = new List<FlowButtonLink>()
//                    };

//                    // Harden profile-name config per template type
//                    var isTextTemplate = string.Equals(step.TemplateType, "text_template", StringComparison.OrdinalIgnoreCase);
//                    if (!isTextTemplate)
//                    {
//                        step.UseProfileName = false;
//                        step.ProfileNameSlot = null;
//                    }
//                    else
//                    {
//                        if (!step.UseProfileName)
//                        {
//                            step.ProfileNameSlot = null;
//                        }
//                        else
//                        {
//                            if (!step.ProfileNameSlot.HasValue || step.ProfileNameSlot.Value < 1)
//                                step.ProfileNameSlot = 1;
//                        }
//                    }

//                    stepMap[node.Id] = step;
//                    _context.CTAFlowSteps.Add(step);
//                }

//                // 4) Build Links (per-node buttons, matched by SourceHandle == button text)
//                var edges = dto.Edges ?? new List<FlowEdgeDto>();

//                foreach (var node in dto.Nodes)
//                {
//                    if (string.IsNullOrWhiteSpace(node.Id) || !stepMap.TryGetValue(node.Id, out var fromStep))
//                        continue;

//                    var outEdges = edges.Where(e => string.Equals(e.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase)).ToList();
//                    var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//                    var orderedButtons = (node.Buttons ?? new List<LinkButtonDto>())
//                        .OrderBy(b => b.Index < 0 ? int.MaxValue : b.Index)
//                        .ThenBy(b => b.Text ?? string.Empty)
//                        .ToList();

//                    short nextIdx = 0;

//                    foreach (var btn in orderedButtons)
//                    {
//                        var text = (btn.Text ?? string.Empty).Trim();
//                        if (string.IsNullOrEmpty(text)) continue;

//                        if (!seenTexts.Add(text))
//                        {
//                            Log.Warning("⚠️ Duplicate button text '{Text}' on node {NodeId}; first wins.", text, node.Id);
//                            continue;
//                        }

//                        var edge = outEdges.FirstOrDefault(e =>
//                            string.Equals(e.SourceHandle ?? string.Empty, text, StringComparison.OrdinalIgnoreCase));
//                        if (edge == null) continue;

//                        if (!stepMap.TryGetValue(edge.ToNodeId, out var toStep)) continue;

//                        var finalIndex = btn.Index >= 0 ? btn.Index : nextIdx;
//                        nextIdx = (short)(finalIndex + 1);

//                        var link = new FlowButtonLink
//                        {
//                            Id = Guid.NewGuid(),
//                            CTAFlowStepId = fromStep.Id,
//                            NextStepId = toStep.Id,
//                            ButtonText = text,
//                            ButtonType = string.IsNullOrWhiteSpace(btn.Type) ? "QUICK_REPLY" : btn.Type,
//                            ButtonSubType = btn.SubType ?? string.Empty,
//                            ButtonValue = btn.Value ?? string.Empty,
//                            ButtonIndex = (short)finalIndex
//                        };

//                        _context.FlowButtonLinks.Add(link);
//                        fromStep.ButtonLinks.Add(link);

//                        // convenience: target step "entry trigger"
//                        toStep.TriggerButtonText = text;
//                        toStep.TriggerButtonType = (btn.Type ?? "QUICK_REPLY").ToLowerInvariant();
//                    }
//                }

//                await _context.SaveChangesAsync();
//                await tx.CommitAsync();

//                Log.Information("✅ Flow created '{Flow}' | Steps: {Steps} | Links: {Links}",
//                    flow.FlowName, stepMap.Count, stepMap.Values.Sum(s => s.ButtonLinks.Count));

//                // Return new flowId so the FE can redirect/open it if desired
//                return ResponseResult.SuccessInfo("✅ Flow created.", new { flowId = flow.Id });
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "❌ Exception while saving (create) flow");
//                return ResponseResult.ErrorInfo("❌ Internal error while saving flow.");
//            }
//        }

//        //public async Task<ResponseResult> SaveVisualFlowAsync(SaveVisualFlowDto dto, Guid businessId, string createdBy)
//        //{
//        //    try
//        //    {
//        //        Log.Information("🧠 SaveVisualFlow started | FlowName: {FlowName} | BusinessId: {BusinessId}", dto.FlowName, businessId);

//        //        if (dto.Nodes == null || !dto.Nodes.Any())
//        //        {
//        //            Log.Warning("❌ No nodes found in flow. Aborting save.");
//        //            return ResponseResult.ErrorInfo("❌ Cannot save an empty flow. Please add at least one step.");
//        //        }

//        //        // 1) Upsert FlowConfig
//        //        var flow = await _context.CTAFlowConfigs
//        //            .FirstOrDefaultAsync(f => f.FlowName == dto.FlowName && f.BusinessId == businessId);

//        //        if (flow == null)
//        //        {
//        //            flow = new CTAFlowConfig
//        //            {
//        //                Id = Guid.NewGuid(),
//        //                BusinessId = businessId,
//        //                FlowName = dto.FlowName,
//        //                CreatedBy = createdBy,
//        //                CreatedAt = DateTime.UtcNow,
//        //                UpdatedAt = DateTime.UtcNow,
//        //                IsActive = true,
//        //                IsPublished = dto.IsPublished
//        //            };
//        //            _context.CTAFlowConfigs.Add(flow);
//        //            Log.Information("✅ New FlowConfig created with ID: {Id}", flow.Id);
//        //        }
//        //        else
//        //        {
//        //            // wipe old steps+links for a clean replace
//        //            var oldSteps = await _context.CTAFlowSteps
//        //                .Where(s => s.CTAFlowConfigId == flow.Id)
//        //                .Include(s => s.ButtonLinks)
//        //                .ToListAsync();

//        //            foreach (var step in oldSteps)
//        //                _context.FlowButtonLinks.RemoveRange(step.ButtonLinks);

//        //            _context.CTAFlowSteps.RemoveRange(oldSteps);

//        //            flow.IsPublished = dto.IsPublished;
//        //            flow.UpdatedAt = DateTime.UtcNow;
//        //        }

//        //        // 2) Build Steps (map by incoming node.Id string)
//        //        var stepMap = new Dictionary<string, CTAFlowStep>(StringComparer.OrdinalIgnoreCase);

//        //        foreach (var (node, index) in dto.Nodes.Select((n, i) => (n, i)))
//        //        {
//        //            if (string.IsNullOrWhiteSpace(node.Id))
//        //                continue;

//        //            var step = new CTAFlowStep
//        //            {
//        //                Id = Guid.NewGuid(),
//        //                CTAFlowConfigId = flow.Id,
//        //                StepOrder = index,
//        //                TemplateToSend = node.TemplateName,
//        //                TemplateType = node.TemplateType ?? "UNKNOWN",
//        //                TriggerButtonText = node.TriggerButtonText ?? "",
//        //                TriggerButtonType = node.TriggerButtonType ?? "cta",
//        //                PositionX = node.PositionX == 0 ? Random.Shared.Next(100, 600) : node.PositionX,
//        //                PositionY = node.PositionY == 0 ? Random.Shared.Next(100, 400) : node.PositionY,
//        //                UseProfileName = node.UseProfileName,
//        //                ProfileNameSlot = node.ProfileNameSlot,
//        //                //ProfileNameSlot = node.ProfileNameSlot ?? 1,
//        //                ButtonLinks = new List<FlowButtonLink>()
//        //            };

//        //            // ✅ Harden profile-name config per step
//        //            var isTextTemplate = string.Equals(step.TemplateType, "text_template", StringComparison.OrdinalIgnoreCase);
//        //            if (!isTextTemplate)
//        //            {
//        //                // Only text templates support body placeholders; disable on others
//        //                step.UseProfileName = false;
//        //                step.ProfileNameSlot = null;
//        //            }
//        //            //else if (step.UseProfileName)
//        //            //{
//        //            //    // Clamp to minimum valid slot
//        //            //    if (!step.ProfileNameSlot.HasValue || step.ProfileNameSlot.Value < 1)
//        //            //        step.ProfileNameSlot = 1;
//        //            //}
//        //            else
//        //            {
//        //                // Text template:
//        //                if (!step.UseProfileName)
//        //                {
//        //                    // Toggle OFF → always null the slot
//        //                    step.ProfileNameSlot = null;
//        //                }
//        //                else
//        //                {
//        //                    // Toggle ON → clamp to minimum valid
//        //                    if (!step.ProfileNameSlot.HasValue || step.ProfileNameSlot.Value < 1)
//        //                        step.ProfileNameSlot = 1;
//        //                    // (Optional) upper clamp if you want: e.g., step.ProfileNameSlot = Math.Min(step.ProfileNameSlot.Value, 50);
//        //                }
//        //            }
//        //            stepMap[node.Id] = step;
//        //            _context.CTAFlowSteps.Add(step);
//        //        }

//        //        // 3) Build Links PER NODE using buttons order (with Index), not per-edge blindly
//        //        foreach (var node in dto.Nodes)
//        //        {
//        //            if (string.IsNullOrWhiteSpace(node.Id) || !stepMap.TryGetValue(node.Id, out var fromStep))
//        //                continue;

//        //            // outgoing edges from this node
//        //            var outEdges = dto.Edges?.Where(e => string.Equals(e.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase)).ToList()
//        //                           ?? new List<FlowEdgeDto>();

//        //            // dedupe by button text to avoid ambiguous routing
//        //            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//        //            // stable ordering: by provided Index (0..N), then by Text
//        //            var orderedButtons = (node.Buttons ?? new List<LinkButtonDto>())
//        //                .OrderBy(b => b.Index < 0 ? int.MaxValue : b.Index)
//        //                .ThenBy(b => b.Text ?? string.Empty)
//        //                .ToList();

//        //            short nextIdx = 0;

//        //            foreach (var btn in orderedButtons)
//        //            {
//        //                var text = (btn.Text ?? string.Empty).Trim();
//        //                if (string.IsNullOrEmpty(text))
//        //                    continue;

//        //                if (!seenTexts.Add(text))
//        //                {
//        //                    Log.Warning("⚠️ Duplicate button text '{Text}' on node {NodeId}; keeping first, skipping duplicates.", text, node.Id);
//        //                    continue;
//        //                }

//        //                // match edge by SourceHandle == button text (how ReactFlow wires handles)
//        //                var edge = outEdges.FirstOrDefault(e =>
//        //                    string.Equals(e.SourceHandle ?? string.Empty, text, StringComparison.OrdinalIgnoreCase));
//        //                if (edge == null)
//        //                {
//        //                    // no wire from this button → skip link creation but keep button metadata in UI on reload
//        //                    continue;
//        //                }

//        //                if (!stepMap.TryGetValue(edge.ToNodeId, out var toStep))
//        //                    continue;

//        //                // final index: prefer incoming payload Index; else fallback to a sequential counter
//        //                var finalIndex = btn.Index >= 0 ? btn.Index : nextIdx;
//        //                nextIdx = (short)(finalIndex + 1);

//        //                var link = new FlowButtonLink
//        //                {
//        //                    Id = Guid.NewGuid(),
//        //                    CTAFlowStepId = fromStep.Id,
//        //                    NextStepId = toStep.Id,
//        //                    ButtonText = text,
//        //                    ButtonType = string.IsNullOrWhiteSpace(btn.Type) ? "QUICK_REPLY" : btn.Type,
//        //                    ButtonSubType = btn.SubType ?? string.Empty,
//        //                    ButtonValue = btn.Value ?? string.Empty,
//        //                    ButtonIndex = (short)finalIndex // 🔑 persist the index
//        //                };

//        //                _context.FlowButtonLinks.Add(link);
//        //                fromStep.ButtonLinks.Add(link);

//        //                // propagate trigger info on the target step for convenience
//        //                toStep.TriggerButtonText = text;
//        //                toStep.TriggerButtonType = (btn.Type ?? "QUICK_REPLY").ToLowerInvariant();
//        //            }
//        //        }

//        //        await _context.SaveChangesAsync();

//        //        Log.Information("✅ Flow '{Flow}' saved | Steps: {StepCount} | Links: {LinkCount}",
//        //            dto.FlowName, stepMap.Count, stepMap.Values.Sum(s => s.ButtonLinks.Count));

//        //        return ResponseResult.SuccessInfo("✅ Flow saved successfully.");
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        Log.Error(ex, "❌ Exception while saving flow");
//        //        return ResponseResult.ErrorInfo("❌ Internal error while saving flow.");
//        //    }
//        //}


//        //public async Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid flowId, Guid businessId)
//        //{
//        //    var flow = await _context.CTAFlowConfigs
//        //        .Include(c => c.Steps)
//        //            .ThenInclude(s => s.ButtonLinks)
//        //        .FirstOrDefaultAsync(c =>
//        //            c.Id == flowId &&
//        //            c.BusinessId == businessId &&   // 👈 tenant scoping
//        //            c.IsActive);

//        //    if (flow == null) return null;

//        //    // ---- Pre-fetch unique template names (defensive) ----
//        //    var templateMap = new Dictionary<string, TemplateMetadataDto>(StringComparer.OrdinalIgnoreCase);
//        //    foreach (var name in flow.Steps
//        //                             .Select(s => s.TemplateToSend)
//        //                             .Where(n => !string.IsNullOrWhiteSpace(n))
//        //                             .Distinct(StringComparer.OrdinalIgnoreCase))
//        //    {
//        //        try
//        //        {
//        //            var tpl = await _templateFetcherService.GetTemplateByNameAsync(
//        //                businessId, name!, includeButtons: true);
//        //            if (tpl != null) templateMap[name!] = tpl;
//        //        }
//        //        catch (Exception ex)
//        //        {
//        //            Log.Warning(ex, "⚠️ Failed to fetch template from Meta for {Template}", name);
//        //        }
//        //    }

//        //    // ---- Nodes ----
//        //    var nodes = flow.Steps.Select(step =>
//        //    {
//        //        templateMap.TryGetValue(step.TemplateToSend ?? "", out var template);

//        //        IEnumerable<FlowButtonLink> links =
//        //            step.ButtonLinks ?? Enumerable.Empty<FlowButtonLink>();

//        //        var dbButtons = links.Select(link => new LinkButtonDto
//        //        {
//        //            Text = link.ButtonText,
//        //            Type = link.ButtonType,
//        //            SubType = link.ButtonSubType,
//        //            Value = link.ButtonValue,
//        //            TargetNodeId = link.NextStepId?.ToString() // null-safe
//        //        });

//        //        var templateButtons = (template?.ButtonParams ?? new List<ButtonMetadataDto>())
//        //            .Where(btn => !links.Any(bl =>
//        //                        string.Equals(bl.ButtonText, btn.Text, StringComparison.OrdinalIgnoreCase)))
//        //            .Select(btn => new LinkButtonDto
//        //            {
//        //                Text = btn.Text,
//        //                TargetNodeId = null
//        //            });

//        //        return new FlowNodeDto
//        //        {
//        //            Id = step.Id.ToString(),
//        //            TemplateName = step.TemplateToSend,
//        //            TemplateType = step.TemplateType,
//        //            MessageBody = template?.Body ?? "Message body preview...",
//        //            TriggerButtonText = step.TriggerButtonText,
//        //            TriggerButtonType = step.TriggerButtonType,
//        //            PositionX = step.PositionX ?? 100,
//        //            PositionY = step.PositionY ?? 100,

//        //            // Conditional logic
//        //            RequiredTag = step.RequiredTag,
//        //            RequiredSource = step.RequiredSource,

//        //            UseProfileName = step.UseProfileName,
//        //            ProfileNameSlot = step.ProfileNameSlot,

//        //            Buttons = dbButtons.Concat(templateButtons).ToList()
//        //        };
//        //    }).ToList();

//        //    // ---- Edges (skip links without a target) ----
//        //    var edges = flow.Steps
//        //        .SelectMany(step =>
//        //            (step.ButtonLinks ?? Enumerable.Empty<FlowButtonLink>())
//        //            .Where(link => link.NextStepId.HasValue)
//        //            .Select(link => new FlowEdgeDto
//        //            {
//        //                FromNodeId = step.Id.ToString(),
//        //                ToNodeId = link.NextStepId!.Value.ToString(),
//        //                SourceHandle = link.ButtonText
//        //            }))
//        //        .ToList();

//        //    return new SaveVisualFlowDto
//        //    {
//        //        FlowName = flow.FlowName,
//        //        IsPublished = flow.IsPublished,
//        //        Nodes = nodes,
//        //        Edges = edges
//        //    };
//        //}

//        public async Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid flowId, Guid businessId)
//        {
//            var flow = await _context.CTAFlowConfigs
//                .Include(c => c.Steps)
//                    .ThenInclude(s => s.ButtonLinks)
//                .FirstOrDefaultAsync(c => c.Id == flowId && c.BusinessId == businessId && c.IsActive);

//            if (flow == null) return null;

//            // 1) Prefetch template metadata for all unique names (defensive, fast)
//            var templateMap = new Dictionary<string, TemplateMetadataDto>(StringComparer.OrdinalIgnoreCase);
//            var uniqueNames = flow.Steps
//                .Select(s => s.TemplateToSend)
//                .Where(n => !string.IsNullOrWhiteSpace(n))
//                .Distinct(StringComparer.OrdinalIgnoreCase)
//                .ToList();

//            foreach (var name in uniqueNames)
//            {
//                try
//                {
//                    var tpl = await _templateFetcherService.GetTemplateByNameAsync(
//                        businessId, name!, includeButtons: true);
//                    if (tpl != null) templateMap[name!] = tpl;
//                }
//                catch (Exception ex)
//                {
//                    Log.Warning(ex, "⚠️ Failed to fetch template from provider for {Template}", name);
//                }
//            }

//            // 2) Build nodes with real body + merged buttons (DB links first, then any unlinked template buttons)
//            var nodes = flow.Steps.Select(step =>
//            {
//                templateMap.TryGetValue(step.TemplateToSend ?? "", out var tpl);

//                var dbLinks = step.ButtonLinks ?? new List<FlowButtonLink>();

//                var dbButtons = dbLinks
//                    .OrderBy(b => b.ButtonIndex)
//                    .Select(link => new LinkButtonDto
//                    {
//                        Text = link.ButtonText,
//                        Type = link.ButtonType,
//                        SubType = link.ButtonSubType,
//                        Value = link.ButtonValue,
//                        Index = link.ButtonIndex,
//                        TargetNodeId = link.NextStepId?.ToString()
//                    });

//                var templateButtons = (tpl?.ButtonParams ?? new List<ButtonMetadataDto>())
//                    .Where(btn => !dbLinks.Any(bl => string.Equals(bl.ButtonText, btn.Text, StringComparison.OrdinalIgnoreCase)))
//                    .Select(btn => new LinkButtonDto
//                    {
//                        Text = btn.Text,
//                        // no TargetNodeId: not wired
//                    });

//                return new FlowNodeDto
//                {
//                    Id = step.Id.ToString(),
//                    TemplateName = step.TemplateToSend,
//                    TemplateType = step.TemplateType,
//                    MessageBody = string.IsNullOrWhiteSpace(tpl?.Body) ? "— no body found —" : tpl!.Body, // ← REAL BODY
//                    TriggerButtonText = step.TriggerButtonText,
//                    TriggerButtonType = step.TriggerButtonType,
//                    PositionX = step.PositionX ?? 100,
//                    PositionY = step.PositionY ?? 100,
//                    RequiredTag = step.RequiredTag,
//                    RequiredSource = step.RequiredSource,
//                    UseProfileName = step.UseProfileName,
//                    ProfileNameSlot = step.ProfileNameSlot,
//                    Buttons = dbButtons.Concat(templateButtons).ToList()
//                };
//            }).ToList();

//            // 3) Build edges
//            var edges = flow.Steps
//                .SelectMany(step => (step.ButtonLinks ?? Enumerable.Empty<FlowButtonLink>())
//                    .Where(l => l.NextStepId.HasValue)
//                    .Select(l => new FlowEdgeDto
//                    {
//                        FromNodeId = step.Id.ToString(),
//                        ToNodeId = l.NextStepId!.Value.ToString(),
//                        SourceHandle = l.ButtonText
//                    }))
//                .ToList();

//            return new SaveVisualFlowDto
//            {
//                FlowName = flow.FlowName,
//                IsPublished = flow.IsPublished,
//                Nodes = nodes,
//                Edges = edges
//            };
//        }

//        public async Task<ResponseResult> DeleteFlowAsync(Guid flowId, Guid businessId, string deletedBy)
//        {
//            // Load flow with children so we can remove in the right order
//            var flow = await _context.CTAFlowConfigs
//                .Include(f => f.Steps)
//                    .ThenInclude(s => s.ButtonLinks)
//                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

//            if (flow == null)
//                return ResponseResult.ErrorInfo("❌ Flow not found or does not belong to you.");

//            // Is this flow used by any active campaign?
//            var inUseQuery = _context.Campaigns
//                .Where(c => c.BusinessId == businessId &&
//                            !c.IsDeleted &&
//                            c.CTAFlowConfigId == flowId);

//            var inUseCount = await inUseQuery.CountAsync();
//            if (inUseCount > 0)
//            {
//                // Optional: show a few campaign names in the error for the UI
//                var sample = await inUseQuery
//                    .OrderByDescending(c => c.CreatedAt)
//                    .Select(c => new { c.Id, c.Name, c.Status })
//                    .Take(5)
//                    .ToListAsync();

//                Log.Warning("❌ Delete flow blocked. Flow {FlowId} is used by {Count} campaigns: {@Sample}",
//                    flowId, inUseCount, sample);

//                return ResponseResult.ErrorInfo(
//                    $"❌ Cannot delete. This flow is attached to {inUseCount} campaign(s). " +
//                    $"Delete those campaigns first."
//                );
//            }

//            // Safe to remove: delete children first, then the flow
//            foreach (var step in flow.Steps)
//                _context.FlowButtonLinks.RemoveRange(step.ButtonLinks);

//            _context.CTAFlowSteps.RemoveRange(flow.Steps);
//            _context.CTAFlowConfigs.Remove(flow);

//            await _context.SaveChangesAsync();
//            return ResponseResult.SuccessInfo("✅ Flow deleted.");
//        }


//        public async Task<List<VisualFlowSummaryDto>> GetAllPublishedFlowsAsync(Guid businessId)
//        {
//            return await _context.CTAFlowConfigs
//                .Where(f => f.BusinessId == businessId && f.IsPublished)
//                .OrderByDescending(f => f.CreatedAt)
//                .Select(f => new VisualFlowSummaryDto
//                {
//                    Id = f.Id,
//                    FlowName = f.FlowName,
//                    IsPublished = f.IsPublished,
//                    CreatedAt = f.CreatedAt
//                })
//                .ToListAsync();
//        }

//        public async Task<List<VisualFlowSummaryDto>> GetAllDraftFlowsAsync(Guid businessId)
//        {
//            return await _context.CTAFlowConfigs
//                .Where(f => f.BusinessId == businessId && !f.IsPublished && f.IsActive)
//                .OrderByDescending(f => f.CreatedAt)
//                .Select(f => new VisualFlowSummaryDto
//                {
//                    Id = f.Id,
//                    FlowName = f.FlowName,
//                    CreatedAt = f.CreatedAt,
//                    IsPublished = f.IsPublished
//                })
//                .ToListAsync();
//        }

//        public async Task<ResponseResult> ExecuteFollowUpStepAsync(Guid businessId, CTAFlowStep? currentStep, string recipientNumber)
//        {
//            // Log.Information("🚀 Executing follow-up for BusinessId: {BusinessId}, CurrentStepId: {StepId}", businessId);
//            if (currentStep == null)
//            {
//                Log.Warning("⚠️ Cannot execute follow-up. Current step is null.");
//                return ResponseResult.ErrorInfo("Current step not found.");
//            }

//            // 🧠 Step: Look through all button links for a valid NextStepId
//            var nextLink = currentStep.ButtonLinks.FirstOrDefault(link => link.NextStepId != null);

//            if (nextLink == null)
//            {
//                Log.Information("ℹ️ No NextStepId defined in any ButtonLinks for StepId: {StepId}", currentStep.Id);
//                return ResponseResult.SuccessInfo("No follow-up step to execute.");
//            }

//            // 🔍 Fetch the next step using new logic (via CTAFlowConfig + Steps)
//            // 1️⃣ Try to resolve with smart condition check
//            var followUpStep = await GetChainedStepAsync(businessId, nextLink.NextStepId, null, null);

//            if (followUpStep == null)
//            {
//                Log.Warning("❌ Follow-up step skipped due to condition mismatch → StepId: {StepId}", nextLink.NextStepId);

//                // 2️⃣ Optional fallback: Try same flow → Any step without conditions
//                var flow = await _context.CTAFlowConfigs
//                    .Include(f => f.Steps)
//                    .FirstOrDefaultAsync(f => f.BusinessId == businessId && f.IsPublished);

//                followUpStep = flow?.Steps
//                    .Where(s => string.IsNullOrEmpty(s.RequiredTag) && string.IsNullOrEmpty(s.RequiredSource))
//                    .OrderBy(s => s.StepOrder)
//                    .FirstOrDefault();

//                if (followUpStep != null)
//                {
//                    Log.Information("🔁 Fallback step selected → StepId: {StepId}, Template: {Template}",
//                        followUpStep.Id, followUpStep.TemplateToSend);
//                }
//                else
//                {
//                    Log.Warning("🚫 No suitable fallback found in flow. Skipping follow-up.");
//                    return ResponseResult.SuccessInfo("No matching follow-up step based on user context.");
//                }
//            }


//            // 📨 Send the follow-up message using the TemplateToSend field
//            try
//            {
//                var template = followUpStep.TemplateToSend;

//                Log.Information("📤 Sending follow-up message → Template: {Template}, To: {Recipient}", template, recipientNumber);

//                // 🧪 Replace this with actual message engine call
//                var sendDto = new SimpleTemplateMessageDto
//                {
//                    RecipientNumber = recipientNumber,
//                    TemplateName = template,
//                    TemplateParameters = new List<string>() // Add dynamic params later if needed
//                };

//                var sendResult = await _messageEngineService
//     .SendTemplateMessageSimpleAsync(businessId, sendDto);

//                if (!sendResult.Success)
//                {
//                    Log.Warning("❌ Follow-up message send failed → {Template}", template);
//                    return ResponseResult.ErrorInfo("Follow-up send failed.", sendResult.ErrorMessage);
//                }


//                return ResponseResult.SuccessInfo($"Follow-up message sent using template: {template}", null, sendResult.RawResponse);

//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "❌ Error sending follow-up message for StepId: {StepId}", followUpStep.Id);
//                return ResponseResult.ErrorInfo("Failed to send follow-up.");
//            }
//        }
//        public Task<CTAFlowStep?> GetChainedStepAsync(Guid businessId, Guid? nextStepId)
//        {
//            return GetChainedStepAsync(businessId, nextStepId, null, null); // Forward to full logic
//        }

//        // ✅ Extended logic with condition check (Tag + Source)
//        public async Task<CTAFlowStep?> GetChainedStepAsync(
//            Guid businessId,
//            Guid? nextStepId,
//            TrackingLog? trackingLog = null,
//            Contact? contact = null)
//        {
//            if (nextStepId == null)
//            {
//                Log.Information("ℹ️ No NextStepId provided — skipping follow-up.");
//                return null;
//            }

//            try
//            {
//                var flow = await _context.CTAFlowConfigs
//                    .Include(f => f.Steps)
//                    .FirstOrDefaultAsync(f =>
//                        f.BusinessId == businessId &&
//                        f.Steps.Any(s => s.Id == nextStepId));

//                if (flow == null)
//                {
//                    Log.Warning("⚠️ No flow found containing NextStepId: {NextStepId} for business: {BusinessId}", nextStepId, businessId);
//                    return null;
//                }

//                var followUpStep = flow.Steps.FirstOrDefault(s => s.Id == nextStepId);

//                if (followUpStep == null)
//                {
//                    Log.Warning("❌ Step matched in flow but not found in step list: {NextStepId}", nextStepId);
//                    return null;
//                }

//                // ✅ Check RequiredTag / Source match
//                if (trackingLog != null)
//                {
//                    var isMatch = StepMatchingHelper.IsStepMatched(followUpStep, trackingLog, contact);

//                    if (!isMatch)
//                    {
//                        Log.Information("🚫 Step {StepId} skipped due to condition mismatch [Tag: {Tag}, Source: {Source}]",
//                            followUpStep.Id, followUpStep.RequiredTag, followUpStep.RequiredSource);
//                        return null;
//                    }
//                }

//                Log.Information("✅ Follow-up step found and matched → StepId: {StepId}, Template: {Template}",
//                    followUpStep.Id, followUpStep.TemplateToSend);

//                return followUpStep;
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "❌ Exception while fetching chained step for NextStepId: {NextStepId}", nextStepId);
//                throw;
//            }
//        }

//        // ✅ Optional helper for resolving from TrackingLogId
//        public async Task<CTAFlowStep?> GetChainedStepWithContextAsync(
//            Guid businessId,
//            Guid? nextStepId,
//            Guid? trackingLogId)
//        {
//            var log = await _context.TrackingLogs
//                .Include(l => l.Contact)
//                    .ThenInclude(c => c.ContactTags)
//                        .ThenInclude(ct => ct.Tag)
//                .FirstOrDefaultAsync(l => l.Id == trackingLogId);

//            return await GetChainedStepAsync(businessId, nextStepId, log, log?.Contact);
//        }


//        public async Task<ResponseResult> ExecuteVisualFlowAsync(Guid businessId, Guid startStepId, Guid trackingLogId, Guid? campaignSendLogId)
//        {
//            try
//            {
//                Log.Information("🚦 Executing Visual Flow → StepId: {StepId} | TrackingLogId: {TrackingLogId}", startStepId, trackingLogId);

//                // ── local helpers ─────────────────────────────────────────────
//                static string ResolveGreeting(string? profileName, string? contactName)
//                {
//                    var s = (profileName ?? contactName)?.Trim();
//                    return string.IsNullOrEmpty(s) ? "there" : s;
//                }
//                static void EnsureArgsLength(List<string> args, int slot1Based)
//                {
//                    while (args.Count < slot1Based) args.Add(string.Empty);
//                }
//                // ───────────────────────────────────────────────────────────────

//                var log = await _context.TrackingLogs
//                    .Include(l => l.Contact)
//                        .ThenInclude(c => c.ContactTags)
//                            .ThenInclude(ct => ct.Tag)
//                    .FirstOrDefaultAsync(l => l.Id == trackingLogId);

//                if (log == null)
//                {
//                    Log.Warning("❌ TrackingLog not found for ID: {TrackingLogId}", trackingLogId);
//                    return ResponseResult.ErrorInfo("Tracking log not found.");
//                }

//                var step = await GetChainedStepAsync(businessId, startStepId, log, log?.Contact);

//                if (step == null)
//                {
//                    Log.Warning("❌ No flow step matched or conditions failed → StepId: {StepId}", startStepId);
//                    return ResponseResult.ErrorInfo("Step conditions not satisfied.");
//                }

//                // ✅ Build profile-aware args for this step (used for text templates)
//                var args = new List<string>();
//                if (step.UseProfileName && step.ProfileNameSlot is int slot && slot >= 1)
//                {
//                    // Prefer the already-loaded contact on the tracking log; fallback to DB lookup
//                    var contact = log.Contact ?? await _context.Contacts
//                        .AsNoTracking()
//                        .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == (log.ContactPhone ?? ""));

//                    var greet = ResolveGreeting(contact?.ProfileName, contact?.Name);
//                    EnsureArgsLength(args, slot);
//                    args[slot - 1] = greet; // 1-based slot -> 0-based index
//                }

//                ResponseResult sendResult;

//                // This switch block remains unchanged, except we pass args for text templates
//                switch (step.TemplateType?.ToLower())
//                {
//                    case "image_template":
//                        var imageDto = new ImageTemplateMessageDto
//                        {
//                            BusinessId = businessId,
//                            RecipientNumber = log.ContactPhone ?? "",
//                            TemplateName = step.TemplateToSend,
//                            LanguageCode = "en_US"
//                            // If your image templates support body params, you can also pass args here.
//                        };
//                        sendResult = await _messageEngineService.SendImageTemplateMessageAsync(imageDto, businessId);
//                        break;

//                    case "text_template":
//                    default:
//                        var textDto = new SimpleTemplateMessageDto
//                        {
//                            RecipientNumber = log.ContactPhone ?? "",
//                            TemplateName = step.TemplateToSend,
//                            TemplateParameters = args // ✅ inject ProfileName here when configured
//                        };
//                        sendResult = await _messageEngineService.SendTemplateMessageSimpleAsync(businessId, textDto);
//                        break;
//                }

//                // ✅ 2. SAVE the new ID to the log
//                var executionLog = new FlowExecutionLog
//                {
//                    Id = Guid.NewGuid(),
//                    BusinessId = businessId,
//                    StepId = step.Id,
//                    FlowId = step.CTAFlowConfigId,
//                    CampaignSendLogId = campaignSendLogId, // <-- THE NEW VALUE IS SAVED HERE
//                    TrackingLogId = trackingLogId,
//                    ContactPhone = log.ContactPhone,
//                    TriggeredByButton = step.TriggerButtonText,
//                    TemplateName = step.TemplateToSend,
//                    TemplateType = step.TemplateType,
//                    Success = sendResult.Success,
//                    ErrorMessage = sendResult.ErrorMessage,
//                    RawResponse = sendResult.RawResponse,
//                    ExecutedAt = DateTime.UtcNow
//                };

//                _context.FlowExecutionLogs.Add(executionLog);
//                await _context.SaveChangesAsync();

//                if (sendResult.Success)
//                {
//                    Log.Information("✅ Flow step executed → Template: {Template} sent to {To}", step.TemplateToSend, log.ContactPhone);
//                }
//                else
//                {
//                    Log.Warning("❌ Failed to send template from flow → {Reason}", sendResult.ErrorMessage);
//                }

//                return ResponseResult.SuccessInfo($"Flow step executed. Sent: {sendResult.Success}", null, sendResult.RawResponse);
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "❌ Exception during ExecuteVisualFlowAsync()");
//                return ResponseResult.ErrorInfo("Internal error during flow execution.");
//            }
//        }

//        public async Task<FlowButtonLink?> GetLinkAsync(Guid flowId, Guid sourceStepId, short buttonIndex)
//        {
//            return await _context.FlowButtonLinks
//                 .Where(l => l.CTAFlowStepId == sourceStepId
//              && l.NextStepId != null
//              && l.Step.CTAFlowConfigId == flowId
//              && l.ButtonIndex == buttonIndex)
//                .SingleOrDefaultAsync();

//        }
//        //public async Task<IReadOnlyList<AttachedCampaignDto>> GetAttachedCampaignsAsync(Guid flowId, Guid businessId)
//        //{
//        //    return await _context.Campaigns
//        //        .Where(c => c.BusinessId == businessId
//        //                    && !c.IsDeleted
//        //                    && c.CTAFlowConfigId == flowId)
//        //        .OrderByDescending(c => c.CreatedAt)
//        //        .Select(c => new AttachedCampaignDto(c.Id, c.Name, c.Status, c.ScheduledAt))
//        //        .ToListAsync();
//        //}

//        public async Task<ResponseResult> GetVisualFlowAsync(Guid flowId, Guid businessId)
//        {
//            try
//            {
//                // Load the flow + steps + button links (no tracking for view)
//                var flow = await _context.CTAFlowConfigs
//                    .AsNoTracking()
//                    .Where(f => f.IsActive && f.BusinessId == businessId && f.Id == flowId)
//                    .Select(f => new
//                    {
//                        f.Id,
//                        f.FlowName,
//                        f.IsPublished,
//                        Steps = _context.CTAFlowSteps
//                            .Where(s => s.CTAFlowConfigId == f.Id)
//                            .OrderBy(s => s.StepOrder)
//                            .Select(s => new
//                            {
//                                s.Id,
//                                s.StepOrder,
//                                s.TemplateToSend,
//                                s.TemplateType,
//                                s.TriggerButtonText,
//                                s.TriggerButtonType,
//                                s.PositionX,
//                                s.PositionY,
//                                s.UseProfileName,
//                                s.ProfileNameSlot,
//                                Buttons = _context.FlowButtonLinks
//                                    .Where(b => b.CTAFlowStepId == s.Id)
//                                    .OrderBy(b => b.ButtonIndex)
//                                    .Select(b => new
//                                    {
//                                        b.ButtonText,
//                                        b.ButtonType,
//                                        b.ButtonSubType,
//                                        b.ButtonValue,
//                                        b.ButtonIndex,
//                                        b.NextStepId
//                                    })
//                                    .ToList()
//                            })
//                            .ToList()
//                    })
//                    .FirstOrDefaultAsync();

//                if (flow == null)
//                {
//                    return ResponseResult.ErrorInfo("Flow not found.");
//                }

//                // Map to FE shape
//                var nodes = flow.Steps.Select(s => new
//                {
//                    id = s.Id.ToString(), // node id = step id
//                    positionX = s.PositionX ?? 0,
//                    positionY = s.PositionY ?? 0,
//                    templateName = s.TemplateToSend,
//                    templateType = s.TemplateType,
//                    triggerButtonText = s.TriggerButtonText ?? string.Empty,
//                    triggerButtonType = s.TriggerButtonType ?? "cta",
//                    requiredTag = string.Empty,       // not used in your model; keep empty
//                    requiredSource = string.Empty,    // not used; keep empty
//                    useProfileName = s.UseProfileName,
//                    profileNameSlot = (s.ProfileNameSlot.HasValue && s.ProfileNameSlot.Value > 0) ? s.ProfileNameSlot.Value : 1,
//                    buttons = s.Buttons.Select(b => new
//                    {
//                        text = b.ButtonText,
//                        type = b.ButtonType,
//                        subType = b.ButtonSubType,
//                        value = b.ButtonValue,
//                        targetNodeId = b.NextStepId == Guid.Empty ? null : b.NextStepId.ToString(),
//                        index = (int)(b.ButtonIndex)
//                    })
//                });

//                // Build edges from button links
//                var edges = flow.Steps
//                    .SelectMany(s => s.Buttons
//                        .Where(b => b.NextStepId != Guid.Empty)
//                        .Select(b => new
//                        {
//                            fromNodeId = s.Id.ToString(),
//                            toNodeId = b.NextStepId.ToString(),
//                            sourceHandle = b.ButtonText // label/handle = button text
//                        }));

//                var payload = new
//                {
//                    flowName = flow.FlowName,
//                    isPublished = flow.IsPublished,
//                    nodes,
//                    edges
//                };

//                return ResponseResult.SuccessInfo("Flow loaded.", payload);
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "❌ Exception while loading visual flow {FlowId}", flowId);
//                return ResponseResult.ErrorInfo("Internal error while loading flow.");
//            }
//        }
//        public async Task<IReadOnlyList<AttachedCampaignDto>> GetAttachedCampaignsAsync(Guid flowId, Guid businessId)
//        {
//            // base query: attached, non-deleted
//            var q = _context.Campaigns
//                .Where(c => c.BusinessId == businessId && !c.IsDeleted && c.CTAFlowConfigId == flowId);

//            // earliest send per campaign
//            var firstSends = await _context.CampaignSendLogs
//                .Where(s => s.BusinessId == businessId && s.CampaignId != Guid.Empty)
//                .GroupBy(s => s.CampaignId)
//                .Select(g => new { CampaignId = g.Key, FirstSentAt = (DateTime?)g.Min(s => s.CreatedAt) })
//                .ToListAsync();

//            var firstSendMap = firstSends.ToDictionary(x => x.CampaignId, x => x.FirstSentAt);

//            var list = await q
//                .OrderByDescending(c => c.CreatedAt)
//                .Select(c => new
//                {
//                    c.Id,
//                    c.Name,
//                    c.Status,
//                    c.ScheduledAt,
//                    c.CreatedAt,
//                    c.CreatedBy
//                })
//                .ToListAsync();

//            return list.Select(x => new AttachedCampaignDto(
//                x.Id,
//                x.Name,
//                x.Status,
//                x.ScheduledAt,
//                x.CreatedAt,
//                x.CreatedBy,
//                firstSendMap.TryGetValue(x.Id, out var ts) ? ts : null
//            )).ToList();
//        }
//        public async Task<bool> HardDeleteFlowIfUnusedAsync(Guid flowId, Guid businessId)
//        {
//            // Load flow + children
//            var flow = await _context.CTAFlowConfigs
//                .Include(f => f.Steps)
//                    .ThenInclude(s => s.ButtonLinks)
//                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

//            if (flow is null) return false;

//            // Guard: any active campaign still attached?
//            var attached = await _context.Campaigns
//                .Where(c => c.BusinessId == businessId
//                            && !c.IsDeleted
//                            && c.CTAFlowConfigId == flowId)
//                .Select(c => c.Id)
//                .Take(1)
//                .AnyAsync();

//            if (attached) return false;

//            // Hard delete (children first; FK-safe)
//            foreach (var step in flow.Steps)
//                _context.FlowButtonLinks.RemoveRange(step.ButtonLinks);

//            _context.CTAFlowSteps.RemoveRange(flow.Steps);
//            _context.CTAFlowConfigs.Remove(flow);

//            await _context.SaveChangesAsync();
//            return true;
//        }

//        //public async Task<FlowUpdateResult> UpdateVisualFlowAsync(Guid flowId, SaveVisualFlowDto dto, Guid businessId, string user)
//        //{
//        //    var flow = await _context.CTAFlowConfigs
//        //        .Include(f => f.Steps)
//        //            .ThenInclude(s => s.ButtonLinks)
//        //        .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

//        //    if (flow is null)
//        //        return new FlowUpdateResult { Status = "notFound", Message = "Flow not found." };

//        //    var attached = await _context.Campaigns
//        //        .Where(c => !c.IsDeleted && c.BusinessId == businessId && c.CTAFlowConfigId == flowId)
//        //        .Select(c => new { c.Id, c.Name, c.Status, c.ScheduledAt, c.CreatedAt, c.CreatedBy })
//        //        .ToListAsync();

//        //    if (flow.IsPublished && attached.Count > 0)
//        //    {
//        //        return new FlowUpdateResult
//        //        {
//        //            Status = "requiresFork",
//        //            Message = "This flow is published and attached to campaign(s). Create a new draft version.",
//        //            Campaigns = attached
//        //        };
//        //    }

//        //    var needsRepublish = flow.IsPublished && attached.Count == 0;
//        //    if (needsRepublish) flow.IsPublished = false; // flip to draft during edit

//        //    // wipe & rebuild steps (simplest and consistent with your builder payload)
//        //    _context.FlowButtonLinks.RemoveRange(flow.Steps.SelectMany(s => s.ButtonLinks));
//        //    _context.CTAFlowSteps.RemoveRange(flow.Steps);
//        //    await _context.SaveChangesAsync();

//        //    flow.FlowName = string.IsNullOrWhiteSpace(dto.FlowName) ? flow.FlowName : dto.FlowName.Trim();
//        //    flow.UpdatedAt = DateTime.UtcNow;

//        //    var newSteps = new List<CTAFlowStep>();
//        //    var nodeIdToNewGuid = new Dictionary<string, Guid>();

//        //    // 1) create steps with new IDs but keep mapping from incoming node.Id
//        //    foreach (var n in dto.Nodes)
//        //    {
//        //        var stepId = Guid.TryParse(n.Id, out var parsed) ? parsed : Guid.NewGuid();
//        //        nodeIdToNewGuid[n.Id] = stepId;

//        //        var s = new CTAFlowStep
//        //        {
//        //            Id = stepId,
//        //            CTAFlowConfigId = flow.Id,
//        //            TemplateToSend = n.TemplateName ?? string.Empty,
//        //            TemplateType = n.TemplateType,
//        //            TriggerButtonText = n.TriggerButtonText ?? "",
//        //            TriggerButtonType = n.TriggerButtonType ?? "",
//        //            StepOrder = 0,
//        //            RequiredTag = n.RequiredTag,
//        //            RequiredSource = n.RequiredSource,
//        //            PositionX = n.PositionX,
//        //            PositionY = n.PositionY,
//        //            UseProfileName = n.UseProfileName,
//        //            ProfileNameSlot = n.ProfileNameSlot
//        //        };

//        //        s.ButtonLinks = (n.Buttons ?? new List<LinkButtonDto>())
//        //            .Select((b, idx) => new FlowButtonLink
//        //            {
//        //                Id = Guid.NewGuid(),
//        //                CTAFlowStepId = s.Id,
//        //                Step = s,
//        //                ButtonText = b.Text ?? "",
//        //                ButtonType = b.Type ?? "QUICK_REPLY",
//        //                ButtonSubType = b.SubType ?? "",
//        //                ButtonValue = b.Value ?? "",
//        //                ButtonIndex = (short)(b.Index >= 0 ? b.Index : idx),
//        //                NextStepId = string.IsNullOrWhiteSpace(b.TargetNodeId) ? null :
//        //                             (Guid.TryParse(b.TargetNodeId, out var t) ? t : null)
//        //            }).ToList();

//        //        newSteps.Add(s);
//        //    }

//        //    flow.Steps = newSteps;
//        //    await _context.SaveChangesAsync();

//        //    return new FlowUpdateResult { Status = "ok", NeedsRepublish = needsRepublish };
//        //}

//        //public async Task<bool> PublishFlowAsync(Guid flowId, Guid businessId, string user)
//        //{
//        //    var flow = await _context.CTAFlowConfigs
//        //        .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

//        //    if (flow is null) return false;

//        //    // sanity: basic validation can be added here (has steps, etc.)
//        //    flow.IsPublished = true;
//        //    flow.UpdatedAt = DateTime.UtcNow;
//        //    await _context.SaveChangesAsync();
//        //    return true;
//        //}

//        // ---------- FORK (create draft copy) ----------
//        //public async Task<Guid> ForkFlowAsync(Guid flowId, Guid businessId, string user)
//        //{
//        //    var src = await _context.CTAFlowConfigs
//        //        .Include(f => f.Steps)
//        //            .ThenInclude(s => s.ButtonLinks)
//        //        .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);

//        //    if (src is null) return Guid.Empty;

//        //    var dst = new CTAFlowConfig
//        //    {
//        //        Id = Guid.NewGuid(),
//        //        BusinessId = src.BusinessId,
//        //        FlowName = src.FlowName + " (copy)",
//        //        IsActive = true,
//        //        IsPublished = false, // new draft
//        //        CreatedAt = DateTime.UtcNow,
//        //        CreatedBy = user,
//        //        UpdatedAt = DateTime.UtcNow
//        //    };

//        //    var oldToNew = new Dictionary<Guid, Guid>();

//        //    // Clone steps first
//        //    foreach (var s in src.Steps)
//        //    {
//        //        var nsId = Guid.NewGuid();
//        //        oldToNew[s.Id] = nsId;

//        //        var ns = new CTAFlowStep
//        //        {
//        //            Id = nsId,
//        //            CTAFlowConfigId = dst.Id,
//        //            TriggerButtonText = s.TriggerButtonText,
//        //            TriggerButtonType = s.TriggerButtonType,
//        //            TemplateToSend = s.TemplateToSend,
//        //            TemplateType = s.TemplateType,
//        //            StepOrder = s.StepOrder,
//        //            RequiredTag = s.RequiredTag,
//        //            RequiredSource = s.RequiredSource,
//        //            PositionX = s.PositionX,
//        //            PositionY = s.PositionY,
//        //            UseProfileName = s.UseProfileName,
//        //            ProfileNameSlot = s.ProfileNameSlot,
//        //            ButtonLinks = new List<FlowButtonLink>()
//        //        };

//        //        dst.Steps.Add(ns);
//        //    }

//        //    // Clone links and rewire targets if possible
//        //    foreach (var s in src.Steps)
//        //    {
//        //        var ns = dst.Steps.First(x => x.Id == oldToNew[s.Id]);
//        //        foreach (var b in s.ButtonLinks.OrderBy(x => x.ButtonIndex))
//        //        {
//        //            ns.ButtonLinks.Add(new FlowButtonLink
//        //            {
//        //                Id = Guid.NewGuid(),
//        //                CTAFlowStepId = ns.Id,
//        //                Step = ns,
//        //                ButtonText = b.ButtonText,
//        //                ButtonType = b.ButtonType,
//        //                ButtonSubType = b.ButtonSubType,
//        //                ButtonValue = b.ButtonValue,
//        //                ButtonIndex = b.ButtonIndex,
//        //                NextStepId = b.NextStepId.HasValue && oldToNew.ContainsKey(b.NextStepId.Value)
//        //                    ? oldToNew[b.NextStepId.Value]
//        //                    : null
//        //            });
//        //        }
//        //    }

//        //    _context.CTAFlowConfigs.Add(dst);
//        //    await _context.SaveChangesAsync();
//        //    return dst.Id;
//        //}


//    }
//}


