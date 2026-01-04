using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CampaignTracking.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignTracking.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CampaignAnalyticsController : BusinessControllerBase
    {
        private readonly ICampaignAnalyticsService _campaignAnalyticsService;
        public CampaignAnalyticsController(ICampaignAnalyticsService svc) => _campaignAnalyticsService = svc;

        [HttpGet("top-campaigns")]
        public async Task<IActionResult> GetTopCampaigns([FromQuery] int count = 5)
            => Ok(await _campaignAnalyticsService.GetTopCampaignsAsync(BusinessId, count));
    }
}

