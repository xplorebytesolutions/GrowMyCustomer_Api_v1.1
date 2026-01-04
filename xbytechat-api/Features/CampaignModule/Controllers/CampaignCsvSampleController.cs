// 📄 File: Features/CampaignModule/Controllers/CampaignCsvSampleController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.Import;   // <-- use the builder
using xbytechat.api.Shared;
using xbytechat_api.WhatsAppSettings.Services;       // User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/csv-sample")]
    [Authorize]
    public sealed class CampaignCsvSampleController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CampaignCsvSchemaBuilder _schemaBuilder;

        public CampaignCsvSampleController(AppDbContext db, CampaignCsvSchemaBuilder schemaBuilder)
        {
            _db = db;
            _schemaBuilder = schemaBuilder;
        }

        // === Canonical schema (delegates to builder) ===
        // GET /api/campaigns/{campaignId}/csv-sample/schema
        [HttpGet("schema")]
        public async Task<IActionResult> GetSchema([FromRoute] Guid campaignId, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // Use canonical builder (parameterN, header.text_paramN, button{1..3}.url_param)
            IReadOnlyList<string> headers;
            try
            {
                headers = await _schemaBuilder.BuildAsync(businessId, campaignId, ct);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            // For backward compatibility with your FE contract:
            // placeholderCount = number of BODY positional placeholders (parameterN)
            var placeholderCount = headers.Count(h => h.StartsWith("parameter", StringComparison.OrdinalIgnoreCase));

            // Infer header meta for convenience
            var headerSlots = headers.Count(h => h.StartsWith("header.text_param", StringComparison.OrdinalIgnoreCase));
            var headerType = headerSlots > 0 ? "text" : "none";
            var needsUrl = false; // only true for image/video/doc templates; leave false here (data-only endpoint)

            return Ok(new
            {
                headers,
                placeholderCount,
                header = new { type = headerType, needsUrl }
            });
        }

        // GET /api/campaigns/{campaignId}/csv-sample
        // Generates a dynamic sample based on the actual campaign schema
        [HttpGet]
        public async Task<IActionResult> GetDynamicSample([FromRoute] Guid campaignId, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            IReadOnlyList<string> headers;
            try
            {
                headers = await _schemaBuilder.BuildAsync(businessId, campaignId, ct);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            // Create a dummy row
            var row = new List<string>();
            foreach (var h in headers)
            {
                var lower = h.ToLowerInvariant();
                if (lower.Contains("phone")) row.Add("1234567890");
                else if (lower.Contains("parameter")) row.Add("value");
                else if (lower.Contains("url")) row.Add("https://example.com");
                else row.Add("abc");
            }
            sb.AppendLine(string.Join(",", row));

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"sample_{campaignId}.csv");
        }

        // GET /api/campaigns/{campaignId}/csv-sample/sample (Legacy/Generic)
        [HttpGet("sample")]
        public IActionResult DownloadSample([FromQuery] int bodyParams = 2, [FromQuery] int headerTextParams = 1, [FromQuery] int urlButtons = 1)
        {
            bodyParams = Math.Max(0, Math.Min(10, bodyParams));        // safety caps
            headerTextParams = Math.Max(0, Math.Min(5, headerTextParams));
            urlButtons = Math.Max(0, Math.Min(3, urlButtons));

            var headers = new List<string> { "phone" };

            for (int i = 1; i <= bodyParams; i++) headers.Add($"parameter{i}");
            for (int i = 1; i <= headerTextParams; i++) headers.Add($"header.text_param{i}");
            for (int i = 1; i <= urlButtons; i++) headers.Add($"button{i}.url_param");

            var example = new List<string> { "+911234567890" };
            for (int i = 1; i <= bodyParams; i++) example.Add($"body_value_{i}");
            for (int i = 1; i <= headerTextParams; i++) example.Add($"header_text_{i}");
            for (int i = 1; i <= urlButtons; i++) example.Add($"https://example.com/order/{{id}}");

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));
            sb.AppendLine(string.Join(",", example.Select(v => v.Replace(",", " "))));

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "campaign_sample.csv");
        }
    }
}
