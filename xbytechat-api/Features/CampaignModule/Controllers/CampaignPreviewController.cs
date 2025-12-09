using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/preview")]
    [Authorize]
    public class CampaignPreviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICampaignPreviewService _preview;

        public CampaignPreviewController(AppDbContext db, ICampaignPreviewService preview)
        {
            _db = db; _preview = preview;
        }

        [HttpPost]
        public async Task<ActionResult<CampaignPreviewResponseDto>> Preview(Guid campaignId, [FromBody] CampaignPreviewRequestDto req)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var exists = await _db.Campaigns.AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId);
            if (!exists) return NotFound();

            var data = await _preview.PreviewAsync(businessId, campaignId, req?.ContactId);
            return Ok(data);
        }
    }
}
