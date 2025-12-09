using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Pinnacle
{
    public sealed record PinnacleTemplateMessage(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("template")] PinnacleTemplate Template
    );

    public sealed record PinnacleTemplate(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] PinnacleLanguage Language,
        [property: JsonPropertyName("components")] IReadOnlyList<PinnacleComponentBase>? Components
    );

    public sealed record PinnacleLanguage([property: JsonPropertyName("code")] string Code);
}
