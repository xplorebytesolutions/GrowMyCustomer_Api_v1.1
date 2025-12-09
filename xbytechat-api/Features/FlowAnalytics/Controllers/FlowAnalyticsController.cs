using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.FlowAnalytics.Services;

namespace xbytechat.api.Features.FlowAnalytics.Controllers
{
    [ApiController]
    [Route("api/flow-analytics")]
    public class FlowAnalyticsController : ControllerBase
    {
        private readonly IFlowAnalyticsService _analyticsService;

        public FlowAnalyticsController(IFlowAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        // ✅ GET /api/flow-analytics/summary
        [HttpGet("summary")]
        [Authorize]
        public async Task<IActionResult> GetSummary()
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;

            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest("❌ Invalid business ID");

            var result = await _analyticsService.GetAnalyticsSummaryAsync(businessId);
            return Ok(result);
        }

        // ✅ GET /api/flow-analytics/most-triggered-steps
        [HttpGet("most-triggered-steps")]
        [Authorize]
        public async Task<IActionResult> GetMostTriggeredSteps()
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;

            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest("❌ Invalid business ID");

            var data = await _analyticsService.GetMostTriggeredStepsAsync(businessId);
            return Ok(data);
        }

        // ✅ GET /api/flow-analytics/step-journey-breakdown?startDate=...&endDate=...
        [HttpGet("step-journey-breakdown")]
        [Authorize]
        public async Task<IActionResult> GetStepJourneyBreakdown([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;

            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest("❌ Invalid business ID");

            var data = await _analyticsService.GetStepJourneyBreakdownAsync(businessId, startDate, endDate);
            return Ok(data);
        }
    }
}
