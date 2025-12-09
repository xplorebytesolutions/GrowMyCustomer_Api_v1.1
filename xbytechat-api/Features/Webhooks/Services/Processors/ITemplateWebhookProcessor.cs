using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public interface ITemplateWebhookProcessor
    {
        Task ProcessTemplateUpdateAsync(JsonElement payload);
    }
}
