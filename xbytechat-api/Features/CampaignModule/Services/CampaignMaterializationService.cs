using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.WhatsAppSettings.Services; // ensure namespace matches your project
using xbytechat.api.Features.MessageManagement.Services;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.WhatsAppSettings.DTOs; // IUrlBuilderService

namespace xbytechat.api.Features.CampaignModule.Services
{
    

    /// <summary>
    /// Read-only “compiler” that materializes template params and button URLs per recipient.
    /// Mirrors the live send behavior (no dispatch, no DB writes).
    /// </summary>
    public sealed class CampaignMaterializationService : ICampaignMaterializationService
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppTemplateFetcherService _templateFetcher;
        private readonly IUrlBuilderService _urlBuilderService;

        private static readonly Regex PlaceholderRe = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);

        public CampaignMaterializationService(
            AppDbContext db,
            IWhatsAppTemplateFetcherService templateFetcher,
            IUrlBuilderService urlBuilderService)
        {
            _db = db;
            _templateFetcher = templateFetcher;
            _urlBuilderService = urlBuilderService;
        }

        //public async Task<CampaignMaterializeResultDto> MaterializeAsync(
        //    Guid businessId,
        //    Guid campaignId,
        //    int limit = 200,
        //    CancellationToken ct = default)
        //{
        //    if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
        //    if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required");
        //    if (limit <= 0) limit = 200;

        //    // Load campaign + variable maps + buttons + recipients (+ contacts)
        //    var campaign = await _db.Campaigns
        //        .AsNoTracking()
        //        .Include(c => c.VariableMaps)
        //        .Include(c => c.MultiButtons)
        //        .Include(c => c.Recipients).ThenInclude(r => r.Contact)
        //        .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);

        //    if (campaign == null)
        //        throw new KeyNotFoundException("Campaign not found.");

        //    // Resolve template meta from snapshot/params; fallback to live
        //    var meta = await ResolveTemplateMetaAsync(campaign, businessId, ct);
        //    var templateName = meta.TemplateName;
        //    var language = meta.Language;
        //    var placeholderCount = meta.PlaceholderCount;

        //    if (string.IsNullOrWhiteSpace(templateName))
        //        throw new InvalidOperationException("Campaign does not have a resolvable template name.");

        //    // Try to fetch provider button meta (for dynamic URL detection & alignment)
        //    TemplateMetadataDto? liveMeta = null;
        //    try
        //    {
        //        liveMeta = await _templateFetcher.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Warning(ex, "Template fetch failed during materialization for {Template}", templateName);
        //    }

        //    var result = new CampaignMaterializeResultDto
        //    {
        //        CampaignId = campaignId,
        //        TemplateName = templateName,
        //        Language = language,
        //        PlaceholderCount = placeholderCount
        //    };

        //    var varMaps = (campaign.VariableMaps ?? new List<CampaignVariableMap>())
        //        .Where(m => m.CampaignId == campaignId)
        //        .ToList();

        //    var recipients = (campaign.Recipients ?? new List<CampaignRecipient>())
        //        .OrderBy(r => r.UpdatedAt)
        //        .Take(limit)
        //        .ToList();

        //    // order buttons by Position (then by their original index) to align with template button index
        //    var orderedButtons = (campaign.MultiButtons ?? new List<CampaignButton>())
        //        .Select((b, idx) => new { Btn = b, idx })
        //        .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
        //        .ThenBy(x => x.idx)
        //        .Select(x => x.Btn)
        //        .ToList();

        //    foreach (var r in recipients)
        //    {
        //        var row = new MaterializedRecipientDto
        //        {
        //            RecipientId = r.Id,
        //            ContactId = r.ContactId,
        //            Phone = NormalizePhone(r?.Contact?.PhoneNumber)
        //        };

        //        // Parameters 1..N via variable maps (Static/Default/Expression placeholder)
        //        for (int idx = 1; idx <= placeholderCount; idx++)
        //        {
        //            var map = varMaps.FirstOrDefault(m => m.Index == idx);
        //            if (map == null)
        //            {
        //                row.Parameters.Add(new TemplateParamResolutionDto
        //                {
        //                    Index = idx,
        //                    Value = null,
        //                    IsMissing = true,
        //                    SourceType = "Unmapped",
        //                    Note = "No variable map for this placeholder."
        //                });
        //                continue;
        //            }

        //            var (value, isMissing, note) = ResolveValue(map, r);
        //            row.Parameters.Add(new TemplateParamResolutionDto
        //            {
        //                Index = idx,
        //                Value = value,
        //                IsMissing = isMissing,
        //                SourceType = map.SourceType ?? string.Empty,
        //                SourceKey = map.SourceKey
        //            });

        //            if (!string.IsNullOrWhiteSpace(note))
        //                row.Warnings.Add($"{{{{{idx}}}}}: {note}");
        //        }

        //        // Buttons: mirror live send behavior for dynamic URL buttons (index 0..2)
        //        if (liveMeta?.ButtonParams != null && liveMeta.ButtonParams.Count > 0 && orderedButtons.Count > 0)
        //        {
        //            var total = Math.Min(3, Math.Min(orderedButtons.Count, liveMeta.ButtonParams.Count));

        //            for (int i = 0; i < total; i++)
        //            {
        //                var metaBtn = liveMeta.ButtonParams[i];
        //                var subType = (metaBtn.SubType ?? "url").ToLowerInvariant();
        //                var metaParam = metaBtn.ParameterValue?.Trim();

        //                var br = new ButtonResolutionDto
        //                {
        //                    ButtonText = orderedButtons[i]?.Title ?? string.Empty,
        //                    RawTemplateValue = orderedButtons[i]?.Value
        //                };

        //                // We only handle dynamic URL buttons here (consistent with send logic)
        //                if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase))
        //                {
        //                    br.Notes.Add("Non-URL button (no dynamic resolution).");
        //                    row.Buttons.Add(br);
        //                    continue;
        //                }

        //                var isDynamic = !string.IsNullOrWhiteSpace(metaParam) && metaParam.Contains("{{");
        //                if (!isDynamic)
        //                {
        //                    br.Notes.Add("Static URL button (no parameters required by template).");
        //                    row.Buttons.Add(br);
        //                    continue;
        //                }

        //                var btn = orderedButtons[i];
        //                var btnType = (btn?.Type ?? "URL").ToUpperInvariant();
        //                if (!string.Equals(btnType, "URL", StringComparison.OrdinalIgnoreCase))
        //                {
        //                    br.Notes.Add($"Template expects a dynamic URL at index {i}, but campaign button type is '{btn?.Type}'.");
        //                    row.Buttons.Add(br);
        //                    continue;
        //                }

        //                var valueRaw = btn?.Value?.Trim();
        //                if (string.IsNullOrWhiteSpace(valueRaw))
        //                {
        //                    br.Notes.Add($"Template requires a dynamic URL at index {i}, but campaign button value is empty.");
        //                    br.MissingArguments.Add("{{1}}");
        //                    row.Buttons.Add(br);
        //                    continue;
        //                }

        //                // optional phone substitution into destination
        //                var phone = row.Phone ?? "";
        //                var encodedPhone = Uri.EscapeDataString(phone);

        //                var resolvedDestination = valueRaw.Contains("{{1}}")
        //                    ? valueRaw.Replace("{{1}}", encodedPhone)
        //                    : valueRaw;

        //                // normalize/validate URL (allow tel:, wa:, wa.me links)
        //                try
        //                {
        //                    resolvedDestination = NormalizeAbsoluteUrlOrThrowForButton(resolvedDestination, btn!.Title ?? $"Button {i + 1}", i);
        //                }
        //                catch (Exception ex)
        //                {
        //                    br.Notes.Add($"Destination invalid: {ex.Message}");
        //                    row.Buttons.Add(br);
        //                    continue;
        //                }

        //                // Build both styles and pick based on template absolute base rule
        //                var fakeSendLogId = Guid.NewGuid(); // preview-only tokenization
        //                var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(
        //                    fakeSendLogId, i, btn!.Title, resolvedDestination);

        //                var tokenParam = BuildTokenParam(fakeSendLogId, i, btn.Title, resolvedDestination);

        //                var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
        //                var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

        //                br.UsedPlaceholders.Add("{{1}}"); // meta indicates dynamic
        //                br.ResolvedUrl = valueToSend;
        //                row.Buttons.Add(br);
        //            }
        //        }
        //        else
        //        {
        //            // no dynamic buttons in template
        //        }

        //        // Basic phone sanity
        //        if (string.IsNullOrWhiteSpace(row.Phone))
        //            row.Errors.Add("Phone is missing.");
        //        else if (!IsLikelyPhone(row.Phone))
        //            row.Warnings.Add("Phone format looks unusual.");

        //        result.Rows.Add(row);
        //    }

        //    result.ReturnedCount = result.Rows.Count;
        //    result.ErrorCount = result.Rows.Sum(r => r.Errors.Count);
        //    result.WarningCount = result.Rows.Sum(r => r.Warnings.Count)
        //                          + result.Rows.Sum(r => r.Parameters.Count(p => p.IsMissing));

        //    Log.Information("Campaign materialization computed {@Summary}",
        //        new
        //        {
        //            campaignId,
        //            businessId,
        //            result.ReturnedCount,
        //            result.ErrorCount,
        //            result.WarningCount,
        //            result.PlaceholderCount,
        //            result.TemplateName,
        //            result.Language
        //        });

        //    return result;
        //}
        public async Task<CampaignMaterializeResultDto> MaterializeAsync(
    Guid businessId,
    Guid campaignId,
    int limit = 200,
    CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required");
            if (limit <= 0) limit = 200;

            // Load campaign + variable maps + buttons + recipients (+ contacts)
            var campaign = await _db.Campaigns
                .AsNoTracking()
                .Include(c => c.VariableMaps)
                .Include(c => c.MultiButtons)
                .Include(c => c.Recipients).ThenInclude(r => r.Contact)
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);

            if (campaign == null)
                throw new KeyNotFoundException("Campaign not found.");

            // Resolve template meta from snapshot/params; fallback to live
            var meta = await ResolveTemplateMetaAsync(campaign, businessId, ct);
            var templateName = meta.TemplateName;
            var language = meta.Language;
            var placeholderCount = meta.PlaceholderCount;

            if (string.IsNullOrWhiteSpace(templateName))
                throw new InvalidOperationException("Campaign does not have a resolvable template name.");

            // Try to fetch provider button meta (for dynamic URL detection & alignment)
            TemplateMetadataDto? liveMeta = null;
            try
            {
                liveMeta = await _templateFetcher.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Template fetch failed during materialization for {Template}", templateName);
            }

            var result = new CampaignMaterializeResultDto
            {
                CampaignId = campaignId,
                TemplateName = templateName,
                Language = language,
                PlaceholderCount = placeholderCount
            };

            var varMaps = (campaign.VariableMaps ?? new List<CampaignVariableMap>())
                .Where(m => m.CampaignId == campaignId)
                .ToList();

            var recipients = (campaign.Recipients ?? new List<CampaignRecipient>())
                .OrderBy(r => r.UpdatedAt)
                .Take(limit)
                .ToList();

            // order buttons by Position (then by their original index) to align with template button index
            var orderedButtons = (campaign.MultiButtons ?? new List<CampaignButton>())
                .Select((b, idx) => new { Btn = b, idx })
                .OrderBy(x => (int?)x.Btn.Position ?? int.MaxValue)
                .ThenBy(x => x.idx)
                .Select(x => x.Btn)
                .ToList();

            foreach (var r in recipients)
            {
                var row = new MaterializedRecipientDto
                {
                    RecipientId = r.Id,
                    ContactId = r.ContactId,
                    Phone = NormalizePhone(r?.Contact?.PhoneNumber)
                };

                // 🔧 INLINE TOLERANT CONTACT LOOKUP (no helpers, no DB writes)
                // If the recipient isn't linked to a Contact yet but we have a phone,
                // try to find a Contact by either +E.164 or plain-digits form.
                if ((row.ContactId == null || row.ContactId == Guid.Empty) && !string.IsNullOrWhiteSpace(row.Phone))
                {
                    string pPlus, pPlain;

                    if (row.Phone.StartsWith("+", StringComparison.Ordinal))
                    {
                        var digitsOnly = new string(row.Phone.Skip(1).Where(char.IsDigit).ToArray());
                        pPlus = row.Phone;
                        pPlain = digitsOnly;
                    }
                    else
                    {
                        var digitsOnly = new string(row.Phone.Where(char.IsDigit).ToArray());
                        pPlus = "+" + digitsOnly;
                        pPlain = digitsOnly;
                    }

                    // Only query if we have something meaningful
                    if (!string.IsNullOrWhiteSpace(pPlus) || !string.IsNullOrWhiteSpace(pPlain))
                    {
                        var foundId = await _db.Contacts
                            .AsNoTracking()
                            .Where(c => c.BusinessId == businessId &&
                                        (c.PhoneNumber == pPlus || c.PhoneNumber == pPlain))
                            .Select(c => c.Id)
                            .FirstOrDefaultAsync(ct);

                        if (foundId != Guid.Empty)
                            row.ContactId = foundId; // reflect resolvable linkage in preview
                    }
                }

                // Parameters 1..N via variable maps (Static/Default/Expression placeholder)
                for (int idx = 1; idx <= placeholderCount; idx++)
                {
                    var map = varMaps.FirstOrDefault(m => m.Index == idx);
                    if (map == null)
                    {
                        row.Parameters.Add(new TemplateParamResolutionDto
                        {
                            Index = idx,
                            Value = null,
                            IsMissing = true,
                            SourceType = "Unmapped",
                            Note = "No variable map for this placeholder."
                        });
                        continue;
                    }

                    var (value, isMissing, note) = ResolveValue(map, r);
                    row.Parameters.Add(new TemplateParamResolutionDto
                    {
                        Index = idx,
                        Value = value,
                        IsMissing = isMissing,
                        SourceType = map.SourceType ?? string.Empty,
                        SourceKey = map.SourceKey
                    });

                    if (!string.IsNullOrWhiteSpace(note))
                        row.Warnings.Add($"{{{{{idx}}}}}: {note}");
                }

                // Buttons: mirror live send behavior for dynamic URL buttons (index 0..2)
                if (liveMeta?.ButtonParams != null && liveMeta.ButtonParams.Count > 0 && orderedButtons.Count > 0)
                {
                    var total = Math.Min(3, Math.Min(orderedButtons.Count, liveMeta.ButtonParams.Count));

                    for (int i = 0; i < total; i++)
                    {
                        var metaBtn = liveMeta.ButtonParams[i];
                        var subType = (metaBtn.SubType ?? "url").ToLowerInvariant();
                        var metaParam = metaBtn.ParameterValue?.Trim();

                        var br = new ButtonResolutionDto
                        {
                            ButtonText = orderedButtons[i]?.Title ?? string.Empty,
                            RawTemplateValue = orderedButtons[i]?.Value
                        };

                        // We only handle dynamic URL buttons here (consistent with send logic)
                        if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase))
                        {
                            br.Notes.Add("Non-URL button (no dynamic resolution).");
                            row.Buttons.Add(br);
                            continue;
                        }

                        var isDynamic = !string.IsNullOrWhiteSpace(metaParam) && metaParam.Contains("{{");
                        if (!isDynamic)
                        {
                            br.Notes.Add("Static URL button (no parameters required by template).");
                            row.Buttons.Add(br);
                            continue;
                        }

                        var btn = orderedButtons[i];
                        var btnType = (btn?.Type ?? "URL").ToUpperInvariant();
                        if (!string.Equals(btnType, "URL", StringComparison.OrdinalIgnoreCase))
                        {
                            br.Notes.Add($"Template expects a dynamic URL at index {i}, but campaign button type is '{btn?.Type}'.");
                            row.Buttons.Add(br);
                            continue;
                        }

                        var valueRaw = btn?.Value?.Trim();
                        if (string.IsNullOrWhiteSpace(valueRaw))
                        {
                            br.Notes.Add($"Template requires a dynamic URL at index {i}, but campaign button value is empty.");
                            br.MissingArguments.Add("{{1}}");
                            row.Buttons.Add(br);
                            continue;
                        }

                        // optional phone substitution into destination
                        var phone = row.Phone ?? "";
                        var encodedPhone = Uri.EscapeDataString(phone);

                        var resolvedDestination = valueRaw.Contains("{{1}}")
                            ? valueRaw.Replace("{{1}}", encodedPhone)
                            : valueRaw;

                        // normalize/validate URL (allow tel:, wa:, wa.me links)
                        try
                        {
                            resolvedDestination = NormalizeAbsoluteUrlOrThrowForButton(resolvedDestination, btn!.Title ?? $"Button {i + 1}", i);
                        }
                        catch (Exception ex)
                        {
                            br.Notes.Add($"Destination invalid: {ex.Message}");
                            row.Buttons.Add(br);
                            continue;
                        }

                        // Build both styles and pick based on template absolute base rule
                        var fakeSendLogId = Guid.NewGuid(); // preview-only tokenization
                        var fullTrackedUrl = _urlBuilderService.BuildTrackedButtonUrl(
                            fakeSendLogId, i, btn!.Title, resolvedDestination);

                        var tokenParam = BuildTokenParam(fakeSendLogId, i, btn.Title, resolvedDestination);

                        var templateHasAbsoluteBase = LooksLikeAbsoluteBaseUrlWithPlaceholder(metaParam);
                        var valueToSend = templateHasAbsoluteBase ? tokenParam : fullTrackedUrl;

                        br.UsedPlaceholders.Add("{{1}}"); // meta indicates dynamic
                        br.ResolvedUrl = valueToSend;
                        row.Buttons.Add(br);
                    }
                }
                else
                {
                    // no dynamic buttons in template
                }

                // Basic phone sanity
                if (string.IsNullOrWhiteSpace(row.Phone))
                    row.Errors.Add("Phone is missing.");
                else if (!IsLikelyPhone(row.Phone))
                    row.Warnings.Add("Phone format looks unusual.");

                result.Rows.Add(row);
            }

            result.ReturnedCount = result.Rows.Count;
            result.ErrorCount = result.Rows.Sum(r => r.Errors.Count);
            result.WarningCount = result.Rows.Sum(r => r.Warnings.Count)
                                  + result.Rows.Sum(r => r.Parameters.Count(p => p.IsMissing));

            Log.Information("Campaign materialization computed {@Summary}",
                new
                {
                    campaignId,
                    businessId,
                    result.ReturnedCount,
                    result.ErrorCount,
                    result.WarningCount,
                    result.PlaceholderCount,
                    result.TemplateName,
                    result.Language
                });

            return result;
        }


        // --- Helpers (mirror your send logic where relevant) ---

        private static (string? value, bool isMissing, string? note) ResolveValue(
            CampaignVariableMap map,
            CampaignRecipient recipient)
        {
            var source = (map.SourceType ?? "").Trim();

            switch (source)
            {
                case "Static":
                    {
                        var v = map.StaticValue;
                        var missing = string.IsNullOrWhiteSpace(v) && map.IsRequired;
                        return (v, missing, missing ? "Required static value missing." : null);
                    }

                case "Expression":
                    {
                        // no eval engine; use DefaultValue if provided
                        var v = map.DefaultValue;
                        var note = "Expression present; no evaluation engine configured. Used DefaultValue.";
                        var missing = string.IsNullOrWhiteSpace(v) && map.IsRequired;
                        return (v, missing, missing ? "Required expression result missing (no default provided)." : note);
                    }

                case "AudienceColumn":
                    {
                        // Current CampaignRecipient shape doesn't carry Audience/CSV row data.
                        // If you later link AudienceMember.AttributesJson here, resolve from it.
                        var v = map.DefaultValue;
                        var missing = string.IsNullOrWhiteSpace(v) && map.IsRequired;
                        return (v, missing, "Audience/CSV source not available on CampaignRecipient; used DefaultValue.");
                    }

                default:
                    {
                        var v = map.DefaultValue;
                        var missing = string.IsNullOrWhiteSpace(v) && map.IsRequired;
                        return (v, missing, missing ? "Unrecognized mapping type and no default." : "Unrecognized mapping type; used DefaultValue.");
                    }
            }
        }

        private static string NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var p = raw.Trim();
            if (!p.StartsWith("+")) p = "+" + new string(p.Where(char.IsDigit).ToArray());
            return p;
        }

        private static bool IsLikelyPhone(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var digits = s.Count(char.IsDigit);
            return digits >= 10 && digits <= 15;
        }

        private static string NormalizeAbsoluteUrlOrThrowForButton(string input, string buttonTitle, int buttonIndex)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException($"Destination is required for button '{buttonTitle}' (index {buttonIndex}).");

            var cleaned = new string(input.Trim().Where(c => !char.IsControl(c)).ToArray());
            if (cleaned.Length == 0)
                throw new ArgumentException($"Destination is required for button '{buttonTitle}' (index {buttonIndex}).");

            // Accept tel: / wa: / wa.me deep links
            if (cleaned.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("wa:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase))
            {
                return cleaned;
            }

            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString();
            }

            throw new ArgumentException(
                $"Destination must be an absolute http/https/tel/wa URL for button '{buttonTitle}' (index {buttonIndex}). Got: '{input}'");
        }

        private static bool LooksLikeAbsoluteBaseUrlWithPlaceholder(string? templateUrl)
        {
            if (string.IsNullOrWhiteSpace(templateUrl)) return false;
            var s = templateUrl.Trim();
            if (!s.Contains("{{")) return false;
            var probe = s.Replace("{{1}}", "x").Replace("{{0}}", "x");
            return Uri.TryCreate(probe, UriKind.Absolute, out var abs) &&
                   (abs.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    abs.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private string BuildTokenParam(Guid campaignSendLogId, int buttonIndex, string? buttonTitle, string destinationUrlAbsolute)
        {
            var full = _urlBuilderService.BuildTrackedButtonUrl(campaignSendLogId, buttonIndex, buttonTitle, destinationUrlAbsolute);
            var pos = full.LastIndexOf("/r/", StringComparison.OrdinalIgnoreCase);
            return (pos >= 0) ? full[(pos + 3)..] : full;
        }

        private sealed record ResolvedTemplateMeta(string TemplateName, string Language, int PlaceholderCount);

       

        private static int CountPlaceholders(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            // matches {{1}}, {{ 2 }}, etc.
            return Regex.Matches(text, @"\{\{\s*\d+\s*\}\}").Count;
        }

        private async Task<ResolvedTemplateMeta> ResolveTemplateMetaAsync(
            Campaign campaign,
            Guid businessId,
            CancellationToken ct)
        {
            string templateName = string.Empty;
            string language = "en";
            int placeholderCount = 0;

            // 1) Snapshot first (if stored)
            if (!string.IsNullOrWhiteSpace(campaign.TemplateSchemaSnapshot))
            {
                try
                {
                    using var doc = JsonDocument.Parse(campaign.TemplateSchemaSnapshot);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        templateName = n.GetString() ?? string.Empty;

                    if (root.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String)
                        language = l.GetString() ?? "en";

                    if (root.TryGetProperty("placeholderCount", out var pc) && pc.TryGetInt32(out var snapCount))
                        placeholderCount = snapCount;
                }
                catch { /* non-fatal */ }
            }

            // 2) Prefer stored TemplateParameters count if present
            if (!string.IsNullOrWhiteSpace(campaign.TemplateParameters))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<List<string>>(campaign.TemplateParameters) ?? new();
                    placeholderCount = Math.Max(placeholderCount, arr.Count);
                }
                catch { /* ignore bad param JSON */ }
            }

            // 3) If name still missing, use MessageTemplate as canonical name (fall back to TemplateId)
            if (string.IsNullOrWhiteSpace(templateName))
                templateName = campaign.MessageTemplate ?? campaign.TemplateId ?? string.Empty;

            // 4) Fallback to live metadata if essentials missing
            if (placeholderCount <= 0 || string.IsNullOrWhiteSpace(templateName))
            {
                try
                {
                    var live = await _templateFetcher.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
                    if (live != null)
                    {
                        if (string.IsNullOrWhiteSpace(templateName) && !string.IsNullOrWhiteSpace(live.Name))
                            templateName = live.Name!;
                        if (!string.IsNullOrWhiteSpace(live.Language))
                            language = live.Language!;

                        // Your TemplateMetadataDto exposes PlaceholderCount and Body (no .Parameters)
                        var liveCount = live.PlaceholderCount > 0
                            ? live.PlaceholderCount
                            : CountPlaceholders(live.Body);

                        placeholderCount = Math.Max(placeholderCount, liveCount);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Live template meta fetch failed for {Template}/{Lang}", templateName, language);
                }
            }

            if (string.IsNullOrWhiteSpace(language)) language = "en";
            if (placeholderCount < 0) placeholderCount = 0;

            return new ResolvedTemplateMeta(templateName, language, placeholderCount);
        }

    }
}
