using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public interface IStatusWebhookProcessor
    {
        Task ProcessStatusUpdateAsync(JsonElement payload, CancellationToken ct = default);
    }
    
}
