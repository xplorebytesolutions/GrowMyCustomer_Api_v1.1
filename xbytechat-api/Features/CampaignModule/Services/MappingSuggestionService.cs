using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.Models;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Heuristics:
    /// - If Campaign.TemplateParameters is a JSON array of tokens, suggest for those tokens.
    /// - Else, derive tokens by normalizing CSV headers (lowercase, alnum only, '_' joined).
    /// - Match by normalized equality/contains; special-case phone names.
    /// - Unmatched tokens get "static:" so UI shows a clear placeholder.
    /// </summary>
    public sealed class MappingSuggestionService : IMappingSuggestionService
    {
        private readonly AppDbContext _db;

        private static readonly string[] PhoneHeaderCandidates =
        {
            "phone", "mobile", "whatsapp", "msisdn", "whatsapp_number", "contact", "contact_number"
        };

        public MappingSuggestionService(AppDbContext db) => _db = db;

        public async Task<Dictionary<string, string>> SuggestAsync(
            Guid businessId,
            Guid campaignId,
            Guid batchId,
            CancellationToken ct = default)
        {
            // Load campaign to read TemplateParameters (if present)
            var campaign = await _db.Campaigns.AsNoTracking()
                .Where(c => c.Id == campaignId && c.BusinessId == businessId)
                .Select(c => new { c.Id, c.BusinessId, c.TemplateParameters })
                .FirstOrDefaultAsync(ct);

            if (campaign == null) throw new KeyNotFoundException("Campaign not found.");

            // Load batch headers
            var batch = await _db.CsvBatches.AsNoTracking()
                .Where(b => b.Id == batchId && b.BusinessId == businessId)
                .Select(b => new { b.HeadersJson })
                .FirstOrDefaultAsync(ct);

            if (batch == null) throw new KeyNotFoundException("CSV batch not found.");

            var headers = ParseHeaders(batch.HeadersJson);
            var normHeaders = headers.ToDictionary(h => Normalize(h), h => h, StringComparer.OrdinalIgnoreCase);

            // Determine tokens
            var tokens = ParseTemplateTokens(campaign.TemplateParameters);
            if (tokens.Count == 0)
            {
                // Fall back: derive tokens directly from headers
                tokens = headers.Select(Normalize).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Suggestion: token -> source
            var suggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in tokens)
            {
                var normToken = Normalize(token);

                // 1) direct equality with header
                if (normHeaders.TryGetValue(normToken, out var exactHeader))
                {
                    suggestions[token] = $"csv:{exactHeader}";
                    continue;
                }

                // 2) phone special-case
                if (IsPhoneToken(normToken))
                {
                    var headerPick = headers.FirstOrDefault(h => PhoneHeaderCandidates.Contains(Normalize(h)));
                    if (!string.IsNullOrEmpty(headerPick))
                    {
                        suggestions[token] = $"csv:{headerPick}";
                        continue;
                    }
                }

                // 3) contains / fuzzy-lite
                var contains = headers.FirstOrDefault(h => Normalize(h).Contains(normToken, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(contains))
                {
                    suggestions[token] = $"csv:{contains}";
                    continue;
                }

                // 4) default: static placeholder (UI can highlight to user)
                suggestions[token] = "static:";
            }

            return suggestions;
        }

        private static List<string> ParseHeaders(string? headersJson)
        {
            if (string.IsNullOrWhiteSpace(headersJson)) return new List<string>();
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(headersJson);
                return arr?.Where(h => !string.IsNullOrWhiteSpace(h)).ToList() ?? new List<string>();
            }
            catch
            {
                // Fallback: maybe comma-separated
                return headersJson.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            }
        }

        private static List<string> ParseTemplateTokens(string? templateParametersJson)
        {
            if (string.IsNullOrWhiteSpace(templateParametersJson)) return new List<string>();
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(templateParametersJson);
                return arr?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static bool IsPhoneToken(string normToken)
        {
            if (string.IsNullOrWhiteSpace(normToken)) return false;
            if (PhoneHeaderCandidates.Contains(normToken)) return true;
            return normToken.Contains("phone") || normToken.Contains("mobile") || normToken.Contains("whatsapp");
        }

        private static string Normalize(string s)
        {
            var lowered = (s ?? "").Trim().ToLowerInvariant();
            if (lowered.Length == 0) return lowered;
            var alnum = Regex.Replace(lowered, @"[^a-z0-9]+", "_");
            alnum = Regex.Replace(alnum, "_{2,}", "_").Trim('_');
            return alnum;
        }
    }
}
