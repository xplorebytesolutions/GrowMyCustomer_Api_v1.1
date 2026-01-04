using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace xbytechat.api.Features.CampaignModule.Helpers
{
    public sealed class VariableResolver : IVariableResolver
    {
        // Back-compat: legacy CSV columns -> canonical keys
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

            // Already canonical (parameterN, header.text_paramN, buttonX.url_param, or named body tokens)
            return key;
        }

        public Dictionary<string, string> ResolveVariables(
            IReadOnlyDictionary<string, string> rowData,
            IReadOnlyDictionary<string, string>? mappings)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Fast path: no explicit mappings — just canonicalize incoming CSV keys
            if (mappings == null || mappings.Count == 0)
            {
                foreach (var kv in rowData)
                {
                    var canon = CanonicalizeKey(kv.Key);
                    result[canon] = kv.Value?.Trim() ?? string.Empty;
                }
                return result;
            }

            // With explicit mappings: support constants and canonicalize source columns
            foreach (var (token, srcRaw) in mappings)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;

                // Back-compat: allow mapping keys like "headerpara1"/"buttonpara1" and canonicalize them.
                var canonToken = CanonicalizeKey(token);

                var src = (srcRaw ?? string.Empty).Trim();

                // Allow "constant:VALUE" mapping
                if (src.StartsWith("constant:", StringComparison.OrdinalIgnoreCase))
                {
                    result[canonToken] = src.Substring("constant:".Length).Trim();
                    continue;
                }

                // Canonicalize the source column name (handles legacy headerpara*/buttonpara*)
                var canonSrc = CanonicalizeKey(src);

                if (rowData.TryGetValue(canonSrc, out var vCanon) && vCanon != null)
                {
                    result[canonToken] = vCanon.Trim();
                }
                else if (rowData.TryGetValue(src, out var vRaw) && vRaw != null) // final fallback
                {
                    result[canonToken] = vRaw.Trim();
                }
                else
                {
                    result[canonToken] = string.Empty;
                }
            }

            return result;
        }
    }
}
