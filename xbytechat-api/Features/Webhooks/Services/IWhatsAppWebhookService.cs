using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services
{
    public interface IWhatsAppWebhookService
    {
        Task ProcessStatusUpdateAsync(Guid businessId, string provider, JsonElement payload, CancellationToken ct = default);
   
    
    }
}
