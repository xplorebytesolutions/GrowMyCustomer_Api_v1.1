using System.Text.Json;

namespace xbytechat.api.Features.Webhooks.Pinnacle.Services.Adapters
{
    public interface IPinnacleToMetaAdapter
    {
        /// <summary>Converts provider-native payload to Meta-like envelope:
        /// { "entry":[{ "changes":[{ "value": { ... } }]}] }</summary>
        JsonElement ToMetaEnvelope(JsonElement pinnPayload);
    }
}
