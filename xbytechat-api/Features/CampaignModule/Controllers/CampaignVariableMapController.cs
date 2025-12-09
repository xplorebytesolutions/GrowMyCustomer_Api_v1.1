using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/variables")]
    [Authorize]
    public class CampaignVariableMapController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICampaignVariableMapService _svc;

        public CampaignVariableMapController(AppDbContext db, ICampaignVariableMapService svc)
        { _db = db; _svc = svc; }

        [HttpGet]
        public async Task<IActionResult> Get(Guid campaignId)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var exists = await _db.Campaigns.AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId);
            if (!exists) return NotFound(new { success = false, message = "Campaign not found" });

            var data = await _svc.GetAsync(businessId, campaignId);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        public async Task<IActionResult> Save(Guid campaignId, [FromBody] CampaignVariableMapDto body)
        {
            var businessId = User.GetBusinessId();
            var userName = User.Identity?.Name ?? "system";
            if (businessId == Guid.Empty) return Unauthorized();

            if (body == null) return BadRequest(new { success = false, message = "Body required" });
            body.CampaignId = campaignId;

            var ok = await _svc.SaveAsync(businessId, body, userName);
            return Ok(new { success = ok });
        }
    }
}
