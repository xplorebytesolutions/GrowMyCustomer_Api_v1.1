using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Webhooks.Pinnacle.Services.Adapters;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.Pinnacle.Controllers
{
    [ApiController]
    [Route("api/pinnacle/callback")]
    public sealed class PinnacleWebhookController : ControllerBase
    {
        private readonly IWebhookQueueService _queue;
        private readonly IPinnacleToMetaAdapter _adapter;
        private readonly ILogger<PinnacleWebhookController> _logger;
        private readonly IFailedWebhookLogService _failedWebhookLogService;

        public PinnacleWebhookController(
            IWebhookQueueService queue,
            IPinnacleToMetaAdapter adapter,
            ILogger<PinnacleWebhookController> logger,
            IFailedWebhookLogService failedWebhookLogService)
        {
            _queue = queue;
            _adapter = adapter;
            _logger = logger;
            _failedWebhookLogService = failedWebhookLogService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JsonElement body)
        {
            // Transform to the envelope WhatsAppWebhookDispatcher already expects
            var metaEnvelope = _adapter.ToMetaEnvelope(body);
            var enqueued = _queue.Enqueue(metaEnvelope);
            if (!enqueued)
            {
                await _failedWebhookLogService.LogFailureAsync(new FailedWebhookLogDto
                {
                    FailureType = "QueueOverload",
                    SourceModule = nameof(PinnacleWebhookController),
                    ErrorMessage = "Webhook queue full; payload dropped.",
                    RawJson = body.GetRawText(),
                    CreatedAt = DateTime.UtcNow
                });

                _logger.LogWarning("Queue overload in {Controller}. Payload persisted to FailedWebhookLogs.", nameof(PinnacleWebhookController));
                return Ok(new { received = true });
            }

            _logger.LogInformation("?? Pinnacle payload transformed and enqueued.");
            return Ok(new { received = true });
        }
    }
}
