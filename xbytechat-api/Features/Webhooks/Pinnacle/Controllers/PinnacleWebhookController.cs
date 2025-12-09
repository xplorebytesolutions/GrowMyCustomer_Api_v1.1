using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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

        public PinnacleWebhookController(IWebhookQueueService queue, IPinnacleToMetaAdapter adapter, ILogger<PinnacleWebhookController> logger)
        {
            _queue = queue;
            _adapter = adapter;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement body)
        {
            // Transform to the envelope WhatsAppWebhookDispatcher already expects
            var metaEnvelope = _adapter.ToMetaEnvelope(body);
            _queue.Enqueue(metaEnvelope);
            _logger.LogInformation("📨 Pinnacle payload transformed and enqueued.");
            return Ok(new { received = true });
        }
    }
}
