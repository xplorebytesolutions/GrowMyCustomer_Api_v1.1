using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignModule.SendEngine;
using xbytechat.api.WhatsAppSettings.Helpers; // TemplateJsonHelper

namespace xbytechat.api.Features.CampaignModule.SendEngine
{
    public interface ICampaignSendValidator
    {
        (bool Ok, string? Error) Validate(SendPlan plan, RecipientPlan recipient, TemplateEnvelope env, WhatsAppTemplate tmplRow);
    }

    public sealed class CampaignSendValidator : ICampaignSendValidator
    {
        public (bool Ok, string? Error) Validate(SendPlan plan, RecipientPlan recipient, TemplateEnvelope env, WhatsAppTemplate tmplRow)
        {
            // Summarize the provider-native template JSON to know how many placeholders exist
            var summary = TemplateJsonHelper.SummarizeDetailed(tmplRow.RawJson ?? "{}", tmplRow.Body);

            // ----- BODY: positional placeholders (parameter1..N)
            var bodySlots = summary.BodyParamIndices?.DefaultIfEmpty(0).Max() ?? 0;
            var bodyHave = env.BodyParams?.Count ?? 0;
            if (bodyHave < bodySlots)
            {
                return (false, $"Body parameters missing: need {bodySlots}, got {bodyHave}. Expected keys parameter1..parameter{bodySlots}.");
            }

            // ----- HEADER: text placeholders (header.text_param1..K) only if header is Text
            var isHeaderText = string.Equals(summary.HeaderKind ?? "none", "text", StringComparison.OrdinalIgnoreCase)
                               || plan.HeaderKind == HeaderKind.Text;
            if (isHeaderText)
            {
                var headerSlots = summary.HeaderParamIndices?.DefaultIfEmpty(0).Max() ?? 0;
                var headerHave = env.HeaderParams?.Count ?? 0;
                if (headerHave < headerSlots)
                {
                    return (false, $"Header text parameters missing: need {headerSlots}, got {headerHave}. Expected keys header.text_param1..header.text_param{headerSlots}.");
                }
            }

            // ----- BUTTONS: url parameter per button position present in template
            var urlButtonPositions = summary.Placeholders
                .Where(p => p.Location == PlaceholderLocation.Button &&
                            string.Equals(p.ButtonField, "param", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.ButtonOrder ?? 0)
                .Where(o => o > 0 && o <= 3)
                .Distinct()
                .OrderBy(o => o)
                .ToArray();

            if (urlButtonPositions.Length > 0)
            {
                // Read per-recipient button params (prefer canonical dict, fallback to legacy array)
                var (btnDict, btnList) = ReadButtonParams(env.PerRecipientButtonParamsJson);

                foreach (var pos in urlButtonPositions)
                {
                    // canonical dict key
                    var key = $"button{pos}.url_param";

                    var hasValue =
                        (btnDict != null && btnDict.TryGetValue(key, out var dv) && !string.IsNullOrWhiteSpace(dv))
                        || (btnList != null && btnList.Count >= pos && !string.IsNullOrWhiteSpace(btnList[pos - 1]));

                    if (!hasValue)
                        return (false, $"Missing per-recipient button parameter: '{key}'. Provide a value for the URL button at position {pos}.");
                }
            }

            return (true, null);
        }

        // Returns (dict,list) where exactly one is non-null when data is present
        private static (Dictionary<string, string>? dict, List<string>? list) ReadButtonParams(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return (null, null);

            // try dict first (canonical)
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json!);
                if (dict is { Count: > 0 }) return (dict, null);
            }
            catch { /* ignore */ }

            // fallback to list (legacy)
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json!);
                if (list is { Count: > 0 }) return (null, list);
            }
            catch { /* ignore */ }

            return (null, null);
        }
    }
}
