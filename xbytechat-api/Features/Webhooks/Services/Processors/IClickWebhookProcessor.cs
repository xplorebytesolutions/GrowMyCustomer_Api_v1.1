using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public interface IClickWebhookProcessor
    {
        Task ProcessClickAsync(JsonElement value);
    }
}
