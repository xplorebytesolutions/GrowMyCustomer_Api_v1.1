using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.Controllers
{
    [ApiController]
    [Route("api/webhooks/test")]
    public class WebhookTestController : ControllerBase
    {
        private readonly IWebhookQueueService _queue;

        public WebhookTestController(IWebhookQueueService queue)
        {
            _queue = queue;
        }

        [HttpPost("simulate-failure")]
        public IActionResult SimulateWebhookFailure()
        {
            var fakePayload = new
            {
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                value = new
                                {
                                    // This will cause dispatcher to throw due to invalid structure
                                    unexpected = "🧪 Simulated bad structure"
                                }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(fakePayload);
            var element = JsonDocument.Parse(json).RootElement;

            _queue.Enqueue(element);

            return Ok(new
            {
                message = "✅ Test payload enqueued to simulate failure.",
                enqueued = true
            });
        }
    }
}
