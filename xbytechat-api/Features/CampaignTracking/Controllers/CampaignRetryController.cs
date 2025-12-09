using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignTracking.Services;

namespace xbytechat.api.Features.CampaignTracking.Controllers
{
    [ApiController]
    [Authorize] // ✅ add this
    [Route("api/campaign-retry")]
    public class CampaignRetryController : ControllerBase
    {
        private readonly ICampaignTrackingRetryService _retryService;

        public CampaignRetryController(ICampaignTrackingRetryService retryService)
        {
            _retryService = retryService;
        }

        [HttpPost("{logId}/retry")]
        public async Task<IActionResult> RetrySingle(Guid logId)
        {
            var success = await _retryService.RetrySingleAsync(logId);
            if (!success) return BadRequest(new { message = "Retry failed or not allowed for this log." });
            return Ok(new { success = true, message = "Retry completed." });
        }

        [HttpPost("campaign/{campaignId}/retry-all")]
        public async Task<IActionResult> RetryAllInCampaign(Guid campaignId)
            => Ok(new { success = true, retriedCount = await _retryService.RetryFailedInCampaignAsync(campaignId) });
    }
}
