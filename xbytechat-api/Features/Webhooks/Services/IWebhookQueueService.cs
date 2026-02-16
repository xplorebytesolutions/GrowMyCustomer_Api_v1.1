using System.Text.Json;
using xbytechat.api.Features.Tracking.DTOs;

namespace xbytechat.api.Features.Webhooks.Services
{
    public interface IWebhookQueueService
    {
        bool Enqueue(JsonElement payload);
        ValueTask<JsonElement> DequeueAsync(CancellationToken cancellationToken);
        int GetQueueLength();
    }
}
