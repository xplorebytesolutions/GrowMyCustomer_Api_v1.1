using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.ReportingModule.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.ReportingModule.Controllers
{
    [ApiController]
    [Authorize] // ✅ add this
    [Route("api/reporting/messages")]
    public class MessageAnalyticsController : ControllerBase
    {
        private readonly IMessageAnalyticsService _service;

        public MessageAnalyticsController(IMessageAnalyticsService service)
        {
            _service = service;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentLogs([FromQuery] int limit = 20)
        {
            var businessId = User.GetBusinessId();
            var logs = await _service.GetRecentLogsAsync(businessId, limit);
            return Ok(new { success = true, data = logs });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPaginatedLogs([FromQuery] PaginatedRequest request)
        {
            var businessId = User.GetBusinessId();
            var result = await _service.GetPaginatedLogsAsync(businessId, request);
            return Ok(new { success = true, data = result });
        }
    }
}
