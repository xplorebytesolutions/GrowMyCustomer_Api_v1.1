using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api;

namespace xbytechat.api.Features.Tracking.Controllers
{
    [ApiController]
    [Route("api/tracking/logs")]
    public class TrackingViewerController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TrackingViewerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? campaignId = null)
        {
            var query = _context.TrackingLogs
                .Include(t => t.Campaign) // Optional
                .Include(t => t.Contact)  // Optional
                .OrderByDescending(t => t.ClickedAt)
                .AsQueryable();

            if (campaignId.HasValue)
                query = query.Where(t => t.CampaignId == campaignId);

            var results = await query
                .Select(t => new
                {
                    t.Id,
                    t.ContactPhone,
                    ContactName = t.Contact != null ? t.Contact.Name : "(N/A)",
                    t.ButtonText,
                    t.CTAType,
                    t.SourceType,
                    t.ClickedAt,
                    t.DeviceType,
                    t.Country,
                    CampaignName = t.Campaign != null ? t.Campaign.Name : "(Unknown)"
                })
                .ToListAsync();

            return Ok(new { success = true, data = results });
        }
    }
}

