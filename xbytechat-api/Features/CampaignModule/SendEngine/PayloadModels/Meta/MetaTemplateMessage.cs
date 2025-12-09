using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta
{
    public sealed record MetaTemplateMessage(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("template")] MetaTemplate Template
    );

    public sealed record MetaTemplate(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] MetaLanguage Language,
        //[property: JsonPropertyName("components")] IReadOnlyList<MetaComponentBase>? Components,
        [property: JsonPropertyName("components")] IReadOnlyList<object>? Components
    );

    //public sealed record MetaLanguage([property: JsonPropertyName("code")] string Code);

    public sealed class MetaLanguage
    {
        [JsonPropertyName("code")]
        public string Code { get; init; }

        public MetaLanguage(string code) => Code = code;
    }

}
