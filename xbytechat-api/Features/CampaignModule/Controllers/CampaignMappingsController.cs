using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CampaignModule.Services;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/mappings")]
    [Authorize] // adjust to your auth
    public sealed class CampaignMappingsController : ControllerBase
    {
        private readonly IVariableMappingService _svc;
        private readonly IMappingSuggestionService _suggest;

        public CampaignMappingsController(IVariableMappingService svc, IMappingSuggestionService suggest)
        {
            _svc = svc;
            _suggest = suggest;
        }

        /// <summary>
        /// Returns saved variable mappings for a campaign.
        /// Shape: dictionary of token -> source (e.g., "first_name" -> "csv:First Name")
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAsync(
            [FromRoute] Guid campaignId,
            CancellationToken ct = default)
        {
            var businessId = GetBusinessIdOrThrow();
            var map = await _svc.GetForCampaignAsync(businessId, campaignId, ct);
            return Ok(map ?? new System.Collections.Generic.Dictionary<string, string>());
        }

        /// <summary>
        /// Saves variable mappings for a campaign.
        /// Body: { "tokenA": "csv:HeaderA", "tokenB": "static:Hello", ... }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveAsync(
            [FromRoute] Guid campaignId,
            [FromBody] System.Collections.Generic.Dictionary<string, string> mappings,
            CancellationToken ct = default)
        {
            if (mappings is null)
                return BadRequest("Body cannot be null; send a mapping dictionary.");

            var businessId = GetBusinessIdOrThrow();
            await _svc.SaveAsync(businessId, campaignId, mappings, ct);
            return NoContent();
        }

        /// <summary>
        /// Suggest default mappings from CSV headers and campaign tokens.
        /// GET /api/campaigns/{campaignId}/mappings/suggest?batchId=...
        /// </summary>
        [HttpGet("suggest")]
        public async Task<IActionResult> SuggestAsync(
            [FromRoute] Guid campaignId,
            [FromQuery] Guid batchId,
            CancellationToken ct = default)
        {
            if (batchId == Guid.Empty)
                return BadRequest("batchId is required.");

            var businessId = GetBusinessIdOrThrow();
            var map = await _suggest.SuggestAsync(businessId, campaignId, batchId, ct);
            return Ok(map);
        }

        // -- helpers --

        private Guid GetBusinessIdOrThrow()
        {
            string? raw =
                User?.FindFirst("business_id")?.Value ??
                User?.FindFirst("BusinessId")?.Value ??
                Request.Headers["X-Business-Id"].FirstOrDefault();

            if (!Guid.TryParse(raw, out var id))
                throw new UnauthorizedAccessException(
                    "Business context missing. Pass X-Business-Id header or ensure the business_id claim is present.");

            return id;
        }
    }
}
