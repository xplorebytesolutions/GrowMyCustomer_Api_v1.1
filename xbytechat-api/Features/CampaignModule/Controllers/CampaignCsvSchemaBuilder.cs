using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.WhatsAppSettings.Helpers; // TemplateJsonHelper

namespace xbytechat.api.Features.CampaignModule.Import
{
    /// <summary>
    /// Builds the CSV header schema for a campaign/template.
    /// - BODY placeholders:
    ///     * POSITIONAL => parameter1..N
    ///     * NAMED      => one column per distinct body token name (e.g., name, slug),
    ///                    and we ALSO include parameter1..N for backward compatibility.
    /// - HEADER text placeholders => header.text_param1..K
    /// - Dynamic URL buttons => button{1..3}.url_param (only when template button has {{..}})
    /// - Always includes "phone" (first column).
    /// </summary>
    public sealed class CampaignCsvSchemaBuilder
    {
        private readonly AppDbContext _db;

        public CampaignCsvSchemaBuilder(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<string>> BuildAsync(Guid businessId, Guid campaignId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required.");

            // Resolve template name from Campaign
            var campaign = await _db.Campaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct)
                ?? throw new KeyNotFoundException("Campaign not found.");

            var templateName = (campaign.TemplateId ?? campaign.MessageTemplate ?? "").Trim();
            if (string.IsNullOrWhiteSpace(templateName))
                throw new InvalidOperationException("Campaign does not have a template selected.");

            // Get the freshest active WhatsAppTemplate row for this name
            var tpl = await _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.Name == templateName && t.IsActive)
                .OrderByDescending(t => t.UpdatedAt > t.CreatedAt ? t.UpdatedAt : t.CreatedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException($"Template '{templateName}' not found in cache.");

            // Parse raw JSON canonically (works even if you don’t store the extra JSON columns)
            var summary = TemplateJsonHelper.SummarizeDetailed(tpl.RawJson, tpl.Body);

            // ——— Collect tokens ———
            var bodyCountPositional = summary.BodyParamIndices?.Distinct().Count() ?? 0;
            var bodyNamed = summary.Placeholders
                .Where(p => p.Location == PlaceholderLocation.Body && p.Type == PlaceholderType.Named)
                .Select(p => p.Name!)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var headerTextSlots = (summary.HeaderKind?.Equals("text", StringComparison.OrdinalIgnoreCase) == true)
                ? (summary.HeaderParamIndices?.Distinct().Count() ?? 0)
                : 0;

            // Buttons with a dynamic param ({{..}}) in the parameter field
            var dynamicButtonOrders = summary.Placeholders
                .Where(p => p.Location == PlaceholderLocation.Button && string.Equals(p.ButtonField, "param", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.ButtonOrder ?? 0)
                .Where(o => o > 0 && o <= 3)
                .Distinct()
                .OrderBy(o => o)
                .ToList();

            // ——— Build headers in a stable order ———
            var headers = new List<string> { "phone" };

            // BODY: positional or named
            if (bodyNamed.Count > 0)
            {
                // • preferred: named columns
                headers.AddRange(bodyNamed);
                // • backward compatible: parameter1..N (with N from placeholder count)
                for (int i = 1; i <= Math.Max(bodyCountPositional, summary.PlaceholderCount); i++)
                    headers.Add($"parameter{i}");
            }
            else
            {
                for (int i = 1; i <= bodyCountPositional; i++)
                    headers.Add($"parameter{i}");
            }

            // HEADER text placeholders remain numeric
            for (int i = 1; i <= headerTextSlots; i++)
                headers.Add($"header.text_param{i}");

            // Dynamic URL buttons (at most 3 in Meta)
            foreach (var ord in dynamicButtonOrders)
                headers.Add($"button{ord}.url_param");

            return headers;
        }
    }
}
