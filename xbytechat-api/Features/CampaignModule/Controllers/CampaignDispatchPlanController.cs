using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/dispatch-plan")]
    [Authorize]
    public class CampaignDispatchPlanController : ControllerBase
    {
        private readonly ICampaignDispatchPlannerService _planner;

        public CampaignDispatchPlanController(ICampaignDispatchPlannerService planner)
        {
            _planner = planner;
        }

        /// <summary>
        /// Returns a read-only dispatch plan: batches, offsets, and throttle summary.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(Guid campaignId, [FromQuery] int limit = 2000, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            Log.Information("Dispatch plan requested {@Ctx}", new { businessId, campaignId, limit });

            var data = await _planner.PlanAsync(businessId, campaignId, limit, ct);
            return Ok(new { success = true, data });
        }
    }
}
