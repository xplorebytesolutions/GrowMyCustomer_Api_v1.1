using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using xbytechat.api.Features.CrmAnalytics.Services;
using xbytechat.api.Shared;
using System.Security.Claims;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CrmAnalytics.Controllers
{
    /// <summary>
    /// Handles CRM analytics summary and trends.
    /// </summary>
    [ApiController]
    [Route("api/crm")]
    public class CrmAnalyticsController : ControllerBase
    {
        private readonly ICrmAnalyticsService _crmAnalyticsService;

        public CrmAnalyticsController(ICrmAnalyticsService crmAnalyticsService)
        {
            _crmAnalyticsService = crmAnalyticsService;
        }

        /// <summary>
        /// Returns dashboard summary stats for the CRM.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var businessId = GetBusinessIdFromContext();
            var result = await _crmAnalyticsService.GetSummaryAsync(businessId);
            return Ok(ResponseResult.SuccessInfo("📊 CRM analytics loaded successfully.", result));
        }

        /// <summary>
        /// Returns contacts-added-over-time trend for graph.
        /// </summary>
        [HttpGet("trends/contacts")]
        public async Task<IActionResult> GetContactTrends()
        {
            var businessId = GetBusinessIdFromContext();
            var result = await _crmAnalyticsService.GetContactTrendsAsync(businessId);
            return Ok(ResponseResult.SuccessInfo("📈 Contact trends loaded successfully.", result));
        }

        /// <summary>
        /// Extracts the businessId (Guid) from current user claims.
        /// </summary>
        private Guid GetBusinessIdFromContext()
        {
            return Guid.TryParse(HttpContext.User.FindFirst("BusinessId")?.Value, out var id)
                ? id
                : Guid.Empty;
        }
    }
}
