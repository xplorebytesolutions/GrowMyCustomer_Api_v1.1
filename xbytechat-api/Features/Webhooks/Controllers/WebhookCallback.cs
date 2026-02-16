// ?? File: Features/Webhooks/Controllers/WebhookCallbackController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.Controllers
{
    [ApiController]
    [Route("api/webhookcallback")]
    public class WebhookCallbackController : ControllerBase
    {
        private readonly ILogger<WebhookCallbackController> _logger;
        private readonly IWebhookQueueService _queue;
        private readonly IFailedWebhookLogService _failedWebhookLogService;

        public WebhookCallbackController(
            ILogger<WebhookCallbackController> logger,
            IWebhookQueueService queue,
            IFailedWebhookLogService failedWebhookLogService)
        {
            _logger = logger;
            _queue = queue;
            _failedWebhookLogService = failedWebhookLogService;
        }

        // ? Single POST endpoint: Pinnacle (and others) send responses here
        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public async Task<IActionResult> Post([FromBody] JsonElement payload, CancellationToken ct)
        {
            if (!Request.HasJsonContentType())
            {
                return BadRequest(new { error = "Content-Type must be application/json" });
            }

            try
            {
                var raw = payload.GetRawText();
                _logger.LogInformation("?? Webhook received. bytes={Len}", raw.Length);

                // Clone JsonElement before queueing
                var enqueued = _queue.Enqueue(payload.Clone());
                if (!enqueued)
                {
                    await _failedWebhookLogService.LogFailureAsync(new FailedWebhookLogDto
                    {
                        FailureType = "QueueOverload",
                        SourceModule = nameof(WebhookCallbackController),
                        ErrorMessage = "Webhook queue full; payload dropped.",
                        RawJson = raw,
                        CreatedAt = DateTime.UtcNow
                    });

                    _logger.LogWarning("Queue overload in {Controller}. Payload persisted to FailedWebhookLogs.", nameof(WebhookCallbackController));
                    return Ok(new { received = true });
                }

                // Return 200 OK so provider won’t retry unnecessarily
                return Ok(new { received = true });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("?? Webhook processing cancelled by client.");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? Failed to enqueue webhook payload.");
                return StatusCode(500, new { error = "webhook_enqueue_failed" });
            }
        }
    }

    // Small helper for JSON content-type
    internal static class HttpRequestContentTypeExtensions
    {
        public static bool HasJsonContentType(this HttpRequest request)
        {
            if (request?.ContentType is null) return false;
            return request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
