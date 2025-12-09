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


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using xbytechat.api.Features.CampaignTracking.Services;

//namespace xbytechat.api.Features.CampaignTracking.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    [Authorize]
//    public class CampaignAnalyticsController : ControllerBase
//    {
//        private readonly ICampaignAnalyticsService _campaignAnalyticsService;

//        public CampaignAnalyticsController(ICampaignAnalyticsService campaignAnalyticsService)
//        {
//            _campaignAnalyticsService = campaignAnalyticsService;
//        }

//        [HttpGet("status-dashboard")]
//        //public async Task<IActionResult> GetStatusDashboard()
//        //{
//        //    var businessIdString = User.FindFirstValue("BusinessId");
//        //    if (!Guid.TryParse(businessIdString, out var businessId))
//        //    {
//        //        return Unauthorized("Invalid business identifier.");
//        //    }
//        //    var result = await _campaignAnalyticsService.GetStatusDashboardAsync(businessId);
//        //    return Ok(result);
//        //}

//        [HttpGet("top-campaigns")]
//        public async Task<IActionResult> GetTopCampaigns([FromQuery] int count = 5)
//        {
//            var businessIdString = User.FindFirstValue("BusinessId");
//            if (!Guid.TryParse(businessIdString, out var businessId))
//            {
//                return Unauthorized("Invalid business identifier.");
//            }
//            var result = await _campaignAnalyticsService.GetTopCampaignsAsync(businessId, count);
//            return Ok(result);
//        }
//    }
//}