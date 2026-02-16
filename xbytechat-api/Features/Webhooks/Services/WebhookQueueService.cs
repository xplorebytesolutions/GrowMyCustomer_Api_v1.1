using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.Webhooks.Services
{
    public class WebhookQueueService : IWebhookQueueService
    {
        private const int QueueCapacity = 5000;
        private readonly Channel<JsonElement> _queue;
        private readonly ILogger<WebhookQueueService> _logger;

        public WebhookQueueService(ILogger<WebhookQueueService> logger)
        {
            _logger = logger;

            var options = new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };

            _queue = Channel.CreateBounded<JsonElement>(options);

            _logger.LogInformation(
                "✅ WebhookQueueService initialized with capacity {Capacity}, FullMode={FullMode}, SingleReader={SingleReader}, SingleWriter={SingleWriter}.",
                QueueCapacity,
                options.FullMode,
                options.SingleReader,
                options.SingleWriter
            );
        }

        public bool Enqueue(JsonElement item)
        {
            var length = item.ToString()?.Length ?? 0;
            var count = _queue.Reader.Count;

            if (count >= QueueCapacity)
            {
                _logger.LogWarning(
                    "⚠️ Webhook queue full; oldest queued payload will be dropped to accept new payload. CurrentCount={Count}, PayloadLength={PayloadLength}.",
                    count,
                    length
                );
            }

            if (!_queue.Writer.TryWrite(item))
            {
                _logger.LogWarning(
                    "⚠️ Webhook payload dropped: queue write rejected. CurrentCount={Count}, PayloadLength={PayloadLength}.",
                    _queue.Reader.Count,
                    length
                );
                return false;
            }

            _logger.LogInformation(
                "📥 Enqueued webhook payload successfully. CurrentCount={Count}, PayloadLength={PayloadLength}.",
                _queue.Reader.Count,
                length
            );
            return true;
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

