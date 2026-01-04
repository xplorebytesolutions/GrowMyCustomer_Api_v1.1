using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/campaigns/retarget")]
    public class CampaignRetargetController : BusinessControllerBase
    {
        private readonly ICampaignRetargetService _svc;

        public CampaignRetargetController(ICampaignRetargetService svc)
        {
            _svc = svc;
        }
        [HttpPost]
        public async Task<IActionResult> CreateRetargetCampaign(
            [FromBody] RetargetCampaignRequestDto dto,
            CancellationToken ct)
        {
            var campaignName = dto.Name?.Trim() ?? dto.Campaign?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(campaignName))
            {
                return BadRequest(new { error = "Campaign Name is required for retargeting." });
            }

            var createdBy = UserId.ToString();
            var result = await _svc.CreateRetargetCampaignAsync(BusinessId, dto, createdBy, ct);
            return Ok(result);
        }
    }
}
