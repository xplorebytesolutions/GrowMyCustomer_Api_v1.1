using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog; // ✅ use Serilog like the rest of your services
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Helpers;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Shared.utility;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignPreviewService
    {
        Task<CampaignPreviewResponseDto> PreviewAsync(Guid businessId, Guid campaignId, Guid? contactId);
    }

    public class CampaignPreviewService : ICampaignPreviewService
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppTemplateFetcherService _templateFetcher;
        private readonly IUrlBuilderService _urlBuilder;

        public CampaignPreviewService(
            AppDbContext db,
            IWhatsAppTemplateFetcherService templateFetcher,
            IUrlBuilderService urlBuilder)
        {
            _db = db;
            _templateFetcher = templateFetcher;
            _urlBuilder = urlBuilder;
        }

        public async Task<CampaignPreviewResponseDto> PreviewAsync(Guid businessId, Guid campaignId, Guid? contactId)
        {
            try
            {
                Log.Information("🧪 Preview start | biz={BusinessId} campaign={CampaignId} contactId={ContactId}",
                    businessId, campaignId, contactId);

                var campaign = await _db.Campaigns
                    .AsNoTracking()
                    .Include(c => c.MultiButtons)
                    .Include(c => c.Recipients).ThenInclude(r => r.Contact)
                    .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

                if (campaign == null)
                {
                    Log.Warning("❌ Preview aborted: campaign not found | biz={BusinessId} campaign={CampaignId}",
                        businessId, campaignId);
                    throw new InvalidOperationException("Campaign not found.");
                }

                // Resolve template name (respect flow entry if any)
                var templateName = await ResolveStartTemplateName(businessId, campaign);
                Log.Information("🔎 Preview resolved template | campaign={CampaignId} template={TemplateName}",
                    campaign.Id, templateName);

                // Fetch template meta (body/buttons/lang/header)
                var meta = await _templateFetcher.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
                if (meta == null)
                {
                    Log.Warning("❌ Preview aborted: template metadata not found | biz={BusinessId} template={TemplateName}",
                        businessId, templateName);
                    throw new InvalidOperationException("Template metadata not found.");
                }

                // Prepare parameters/body
                var parsedParams = TemplateParameterHelper.ParseTemplateParams(campaign.TemplateParameters);
                var body = meta.Body ?? campaign.MessageTemplate ?? string.Empty;
                var bodyPreview = TemplateParameterHelper.FillPlaceholders(body, parsedParams);

                // Compute missing params (simple check: count vs supplied)
                var missing = new List<string>();
                if (meta.PlaceholderCount > 0)
                {
                    var supplied = parsedParams?.Count ?? 0;
                    if (supplied < meta.PlaceholderCount)
                    {
                        for (int i = supplied + 1; i <= meta.PlaceholderCount; i++)
                            missing.Add($"{{{{{i}}}}} parameter is missing");

                        Log.Warning("⚠️ Preview found missing params | campaign={CampaignId} required={Required} supplied={Supplied}",
                            campaign.Id, meta.PlaceholderCount, supplied);
                    }
                }

                // Choose contact for dynamic phone substitutions
                var contact = await PickContactAsync(campaign, contactId);

                // Buttons preview
                var buttons = BuildButtonsPreview(campaign, meta, contact);

                var result = new CampaignPreviewResponseDto
                {
                    CampaignId = campaign.Id,
                    TemplateName = templateName,
                    Language = meta.Language ?? "en_US",
                    PlaceholderCount = meta.PlaceholderCount,
                    BodyPreview = bodyPreview,
                    MissingParams = missing,
                    HasHeaderMedia = meta.HasImageHeader,
                    HeaderType = meta.HasImageHeader ? "IMAGE" : null,
                    Buttons = buttons
                };

                Log.Information("✅ Preview ready | campaign={CampaignId} template={TemplateName} placeholders={Count}",
                    campaign.Id, templateName, meta.PlaceholderCount);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "🚨 Preview failed | biz={BusinessId} campaign={CampaignId}", businessId, campaignId);
                throw; // let controller shape the HTTP response (keeps consistency with your pattern)
            }
        }

        // ---------- helpers ----------

        private async Task<string> ResolveStartTemplateName(Guid businessId, Campaign campaign)
        {
            string selected = campaign.TemplateId ?? campaign.MessageTemplate ?? string.Empty;
            if (!campaign.CTAFlowConfigId.HasValue || campaign.CTAFlowConfigId.Value == Guid.Empty)
                return selected;

            var flow = await _db.CTAFlowConfigs
                .Include(f => f.Steps).ThenInclude(s => s.ButtonLinks)
                .FirstOrDefaultAsync(f => f.Id == campaign.CTAFlowConfigId.Value
                                        && f.BusinessId == businessId
                                        && f.IsActive);

            if (flow == null || flow.Steps == null || flow.Steps.Count == 0)
                return selected;

            var incoming = new HashSet<Guid>(flow.Steps
                .SelectMany(s => s.ButtonLinks)
                .Where(l => l.NextStepId.HasValue)
                .Select(l => l.NextStepId!.Value));

            var entry = flow.Steps.OrderBy(s => s.StepOrder)
                                  .FirstOrDefault(s => !incoming.Contains(s.Id));

            return string.IsNullOrWhiteSpace(entry?.TemplateToSend) ? selected : entry!.TemplateToSend!;
        }

        private async Task<Contact?> PickContactAsync(Campaign campaign, Guid? requestedContactId)
        {
            if (requestedContactId.HasValue)
            {
                var specific = campaign.Recipients?.FirstOrDefault(r => r.ContactId == requestedContactId)?.Contact;
                if (specific != null) return specific;

                // allow direct lookup if not in recipients yet
                return await _db.Contacts.FirstOrDefaultAsync(c =>
                    c.Id == requestedContactId.Value && c.BusinessId == campaign.BusinessId);
            }

            // fallback: first recipient’s contact
            return campaign.Recipients?.FirstOrDefault()?.Contact;
        }

        private List<ButtonPreviewDto> BuildButtonsPreview(Campaign campaign, TemplateMetadataDto meta, Contact? contact)
        {
            var result = new List<ButtonPreviewDto>();
            var campaignButtons = campaign.MultiButtons?
                .OrderBy(b => b.Position)
                .Take(3)
                .ToList() ?? new List<CampaignButton>();

            var templateButtons = meta.ButtonParams ?? new List<ButtonMetadataDto>();
            var total = Math.Min(3, Math.Min(campaignButtons.Count, templateButtons.Count));

            for (int i = 0; i < total; i++)
            {
                var tplBtn = templateButtons[i];
                var campBtn = campaignButtons[i];

                var subType = (tplBtn.SubType ?? "url").ToLowerInvariant();
                var baseParam = tplBtn.ParameterValue?.Trim();
                var isDynamic = subType == "url" && !string.IsNullOrWhiteSpace(baseParam) && baseParam.Contains("{{");

                string? token = null;
                string? previewUrl = null;
                string? campaignValue = campBtn.Value?.Trim();

                if (isDynamic && string.IsNullOrWhiteSpace(campaignValue))
                {
                    Log.Warning("⚠️ Preview: dynamic URL button without campaign value | campaign={CampaignId} idx={Index} label={Label}",
                        campaign.Id, i, tplBtn.Text ?? campBtn.Title ?? "");
                }

                if (isDynamic && !string.IsNullOrWhiteSpace(campaignValue))
                {
                    // optional phone substitution
                    var phone = NormalizePhone(contact?.PhoneNumber);
                    var replaced = campaignValue.Contains("{{1}}")
                        ? campaignValue.Replace("{{1}}", Uri.EscapeDataString(phone ?? ""))
                        : campaignValue;

                    // Build tracked URL using a synthetic id (only for preview)
                    var fakeLogId = Guid.NewGuid();
                    var tracked = _urlBuilder.BuildTrackedButtonUrl(fakeLogId, i, campBtn.Title, NormalizeAbsoluteUrl(replaced));
                    previewUrl = tracked;

                    // extract token after "/r/"
                    token = ExtractToken(tracked);
                }

                result.Add(new ButtonPreviewDto
                {
                    Index = i,
                    Text = tplBtn.Text ?? campBtn.Title ?? "",
                    Type = tplBtn.Type ?? "URL",
                    IsDynamic = isDynamic,
                    TemplateParamBase = baseParam,
                    CampaignValue = campaignValue,
                    TokenParam = token,
                    FinalUrlPreview = previewUrl
                });
            }

            return result;
        }

        private static string? NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (!s.StartsWith("+")) s = "+" + new string(s.Where(char.IsDigit).ToArray());
            return s;
        }

        private static string NormalizeAbsoluteUrl(string input)
        {
            // allow tel:/wa: for preview, but tracking expects http(s); if not absolute http(s), keep as-is.
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString();
            }
            return input;
        }

        private static string? ExtractToken(string fullTrackedUrl)
        {
            var pos = fullTrackedUrl.LastIndexOf("/r/", StringComparison.OrdinalIgnoreCase);
            if (pos < 0) return null;
            var token = fullTrackedUrl[(pos + 3)..];
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }
}
