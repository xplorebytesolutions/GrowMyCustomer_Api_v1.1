using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Features.Tracking.Models;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Tracking.Controllers
{
    [ApiController]
    [Route("api/failed-webhooks")]
    public class FailedWebhookLogsController : ControllerBase
    {
        private readonly IFailedWebhookLogService _service;

        public FailedWebhookLogsController(IFailedWebhookLogService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var logs = await _service.GetAllAsync();
            return Ok(logs);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var log = await _service.GetByIdAsync(id);
            if (log == null)
                return NotFound();

            return Ok(log);
        }
    }
}
