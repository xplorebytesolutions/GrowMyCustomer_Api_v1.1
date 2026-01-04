using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.Webhooks.Services
{
    public class WebhookQueueService : IWebhookQueueService
    {
        private readonly Channel<JsonElement> _queue;
        private readonly ILogger<WebhookQueueService> _logger;

        public WebhookQueueService(ILogger<WebhookQueueService> logger)
        {
            _logger = logger;

            var options = new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _queue = Channel.CreateBounded<JsonElement>(options);

            _logger.LogInformation(
                "✅ WebhookQueueService initialized with capacity {Capacity}, FullMode={FullMode}, SingleReader={SingleReader}, SingleWriter={SingleWriter}.",
                5000,
                options.FullMode,
                options.SingleReader,
                options.SingleWriter
            );
        }

        public void Enqueue(JsonElement item)
        {
            // Optional: log size instead of full content to avoid noisy logs
            var length = item.ToString()?.Length ?? 0;

            if (!_queue.Writer.TryWrite(item))
            {
                _logger.LogError(
                    "❌ Failed to enqueue webhook payload: queue is full. CurrentCount={Count}, PayloadLength={PayloadLength}.",
                    _queue.Reader.Count,
                    length
                );
                throw new InvalidOperationException("⚠️ Webhook queue is full.");
            }

            _logger.LogInformation(
                "📥 Enqueued webhook payload successfully. CurrentCount={Count}, PayloadLength={PayloadLength}.",
                _queue.Reader.Count,
                length
            );
        }

        public async ValueTask<JsonElement> DequeueAsync(CancellationToken cancellationToken)
        {
            var item = await _queue.Reader.ReadAsync(cancellationToken);

            // Again, just log length, not the full JSON, to keep logs readable
            var length = item.ToString()?.Length ?? 0;

            _logger.LogInformation(
                "📤 Dequeued webhook payload for processing. RemainingCount={Count}, PayloadLength={PayloadLength}.",
                _queue.Reader.Count,
                length
            );

            return item;
        }

        public int GetQueueLength() => _queue.Reader.Count;
    }
}

