using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services
{
    public interface IWhatsAppWebhookDispatcher
    {
        Task DispatchAsync(JsonElement payload);
    }
}
