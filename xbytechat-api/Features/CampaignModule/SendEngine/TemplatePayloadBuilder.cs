// Features/CampaignModule/SendEngine/TemplatePayloadBuilder.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace xbytechat.api.Features.CampaignModule.SendEngine
{
    public interface ITemplatePayloadBuilder
    {
        TemplateEnvelope Build(SendPlan plan, RecipientPlan r);
    }

    public sealed class TemplatePayloadBuilder : ITemplatePayloadBuilder
    {
        // Back-compat for legacy CSV/header names:
        //   headerpara1     => header.text_param1
        //   buttonpara2     => button2.url_param
        private static readonly Regex RxHeaderPara = new(@"^headerpara(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxButtonPara = new(@"^buttonpara(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string CanonicalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            key = key.Trim();

            var m1 = RxHeaderPara.Match(key);
            if (m1.Success) return $"header.text_param{m1.Groups[1].Value}";

            var m2 = RxButtonPara.Match(key);
            if (m2.Success) return $"button{m2.Groups[1].Value}.url_param";

            return key; // already canonical (parameterN, header.text_paramN, buttonX.url_param, named tokens, etc.)
        }

        private static Dictionary<string, string> NormalizeKeys(Dictionary<string, string> dict)
        {
            var res = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
            {
                var canon = CanonicalizeKey(kv.Key);
                res[canon] = kv.Value ?? string.Empty;
            }
            return res;
        }

        public TemplateEnvelope Build(SendPlan plan, RecipientPlan r)
        {
            var bodyParams = DeserializeStringArray(r.ParametersJson);

            // Parse per-recipient extras (header + button params live here)
            var origJson = r.ButtonParamsJson ?? "{}";
            var looksLikeList = LooksLikeJsonArray(origJson);

            // If dict → normalize legacy keys; if list → keep as-is (we'll align Pinnacle in Step 4)
            var perRecipientDict = DeserializeDict(looksLikeList ? "{}" : origJson);
            var perRecipient = NormalizeKeys(perRecipientDict);

            // Accept both styles: "header.text.1" and "header.text_param1"
            var headerParams =
                ExtractOrdered(perRecipient, "header.text.")
                .Concat(ExtractOrdered(perRecipient, "header.text_param"))
                .ToArray();

            // If input was a dict, write back normalized dict so mappers see canonical keys.
            // If input was a list, preserve original JSON to avoid breaking current Pinnacle behavior.
            var outButtonParamsJson = looksLikeList
                ? origJson
                : JsonSerializer.Serialize(perRecipient);

            return new TemplateEnvelope(
                HeaderKind: plan.HeaderKind,
                HeaderParams: headerParams,
                HeaderUrl: plan.HeaderUrl,
                BodyParams: bodyParams,
                Buttons: plan.Buttons,
                PerRecipientButtonParamsJson: outButtonParamsJson
            );
        }

        private static bool LooksLikeJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            for (int i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                if (char.IsWhiteSpace(ch)) continue;
                return ch == '['; // first non-space
            }
            return false;
        }

        private static string[] DeserializeStringArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try { return JsonSerializer.Deserialize<string[]>(json!) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        private static Dictionary<string, string> DeserializeDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json!) ?? new(); }
            catch { return new(); }
        }

        private static string[] ExtractOrdered(Dictionary<string, string> dict, string prefix)
        {
            return dict
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(kv => (Idx: TryIndex(kv.Key, prefix), Val: kv.Value))
                .Where(x => x.Idx > 0)
                .OrderBy(x => x.Idx)
                .Select(x => x.Val)
                .ToArray();

            static int TryIndex(string key, string prefix)
                => int.TryParse(key.AsSpan(prefix.Length), out var i) ? i : -1;
        }
    }
}
