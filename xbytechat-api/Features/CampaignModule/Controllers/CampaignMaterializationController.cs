using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/materialize")]
    [Authorize]
    public class CampaignMaterializationController : ControllerBase
    {
        private readonly ICampaignMaterializationService _materializer;

        public CampaignMaterializationController(ICampaignMaterializationService materializer)
        {
            _materializer = materializer;
        }

        /// <summary>
        /// Returns a page (limit) of fully materialized recipients: placeholder values and resolved button URLs.
        /// No send, read-only.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(Guid campaignId, [FromQuery] int limit = 200, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            Log.Information("Materialize GET requested {@Ctx}", new { businessId, campaignId, limit });

            var data = await _materializer.MaterializeAsync(businessId, campaignId, limit, ct);
            return Ok(new { success = true, data });
        }
    }
}
