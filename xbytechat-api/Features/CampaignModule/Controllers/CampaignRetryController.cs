using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns")]
    [Authorize]
    public sealed class CampaignRetryController : ControllerBase
    {
        private readonly Services.ICampaignRetryService _retry;

        public CampaignRetryController(Services.ICampaignRetryService retry)
        {
            _retry = retry;
        }

        // POST /api/campaigns/{campaignId}/retry-failed?limit=200
        [HttpPost("{campaignId:guid}/retry-failed")]
        public async Task<IActionResult> RetryFailed([FromRoute] Guid campaignId, [FromQuery] int limit = 200)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty)
                return Unauthorized(new { success = false, error = "Invalid business context." });

            try
            {
                var data = await _retry.RetryFailedAsync(businessId, campaignId, limit);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RetryFailed error for Campaign {CampaignId}", campaignId);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
