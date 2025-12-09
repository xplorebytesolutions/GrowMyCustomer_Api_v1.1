using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns")]
    [Authorize]
    public sealed class CampaignDryRunController : ControllerBase
    {
        private readonly ICampaignService _campaigns;
        public CampaignDryRunController(ICampaignService campaigns) => _campaigns = campaigns;

        // GET /api/campaigns/{campaignId}/dry-run?limit=20
        [HttpGet("{campaignId:guid}/dry-run")]
        public async Task<IActionResult> DryRun([FromRoute] Guid campaignId, [FromQuery] int limit = 20)
        {
            if (campaignId == Guid.Empty) return BadRequest(new { message = "Invalid campaignId" });
            if (limit <= 0) limit = 20;
            if (limit > 200) limit = 200; // guardrails

            var resp = await _campaigns.DryRunTemplateCampaignAsync(campaignId, limit);
            return Ok(resp);
        }
    }
}
