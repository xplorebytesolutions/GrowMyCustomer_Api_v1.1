using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Tracking.Controllers
{
    [ApiController]
    [Route("api/failed-webhooks")]
    public class FailedWebhookLogsController : ControllerBase
    {
        private readonly IFailedWebhookLogService _service;
        private readonly IWebhookQueueService _queue;
        private readonly AppDbContext _context;

        public FailedWebhookLogsController(
            IFailedWebhookLogService service,
            IWebhookQueueService queue,
            AppDbContext context)
        {
            _service = service;
            _queue = queue;
            _context = context;
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

        // Admin-only replay for queue overload payloads.
        [HttpPost("replay/queue-overload")]
        [Authorize(Roles = "admin,superadmin,partner")]
        public async Task<IActionResult> ReplayQueueOverload([FromQuery] int max = 100)
        {
            max = Math.Clamp(max, 1, 500);

            var rows = await _context.FailedWebhookLogs
                .Where(x => x.FailureType == "QueueOverload")
                .OrderBy(x => x.CreatedAt)
                .Take(max)
                .ToListAsync();

            int replayed = 0;
            int parseFailed = 0;

            foreach (var row in rows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.RawJson);
                    var enqueued = _queue.Enqueue(doc.RootElement.Clone());
                    if (!enqueued)
                        break;

                    _context.FailedWebhookLogs.Remove(row);
                    replayed++;
                }
                catch
                {
                    parseFailed++;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                attempted = rows.Count,
                replayed,
                parseFailed,
                remainingQueueDepth = _queue.GetQueueLength()
            });
        }
    }
}
