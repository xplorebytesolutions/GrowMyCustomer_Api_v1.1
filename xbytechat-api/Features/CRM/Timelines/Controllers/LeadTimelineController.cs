using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CRM.Timelines.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeadTimelineController : ControllerBase
    {
        private readonly ILeadTimelineService _timelineService;

        public LeadTimelineController(ILeadTimelineService timelineService)
        {
            _timelineService = timelineService;
        }

        [HttpPost]
        public async Task<IActionResult> AddTimelineEntry([FromBody] LeadTimelineDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _timelineService.AddTimelineEntryAsync(dto);

                Log.Information("✅ Timeline entry created for ContactId: {ContactId}", dto.ContactId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to add timeline entry for ContactId: {ContactId}", dto.ContactId);
                throw;
            }
        }

        [HttpGet("contact/{contactId}")]
        public async Task<IActionResult> GetTimeline(Guid contactId)
        {
            try
            {
                var timeline = await _timelineService.GetTimelineByContactIdAsync(contactId);

                Log.Information("📄 Retrieved {Count} entries for ContactId: {ContactId}", timeline.Count, contactId);

                return Ok(timeline);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to get timeline for ContactId: {ContactId}", contactId);
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var timelines = await _timelineService.GetAllTimelinesAsync();
            return Ok(timelines);
        }
    }
}
