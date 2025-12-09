using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta
{
    // Polymorphic set for Meta (WhatsApp Cloud). "index" is STRING, "sub_type" is snake_case.

    
    [JsonDerivedType(typeof(MetaBodyComponent), typeDiscriminator: "body")]
    [JsonDerivedType(typeof(MetaHeaderTextComponent), typeDiscriminator: "header_text")]
    [JsonDerivedType(typeof(MetaHeaderImageComponent), typeDiscriminator: "header_image")]
    [JsonDerivedType(typeof(MetaHeaderVideoComponent), typeDiscriminator: "header_video")]
    [JsonDerivedType(typeof(MetaHeaderDocumentComponent), typeDiscriminator: "header_document")]
    [JsonDerivedType(typeof(MetaButtonUrlComponent), typeDiscriminator: "button_url")]
    public abstract record MetaComponentBase;

    public sealed record MetaTextParam(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text
    );

    public sealed record MetaMediaParam(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("image")] MetaMediaLink? Image = null,
        [property: JsonPropertyName("video")] MetaMediaLink? Video = null,
        [property: JsonPropertyName("document")] MetaMediaLink? Document = null
    );

    public sealed record MetaMediaLink([property: JsonPropertyName("link")] string Link);

    public sealed record MetaBodyComponent(
        [property: JsonPropertyName("type")] string Type, // "body"
        [property: JsonPropertyName("parameters")] MetaTextParam[] Parameters
    ) : MetaComponentBase;

    public sealed record MetaHeaderTextComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] MetaTextParam[] Parameters
    ) : MetaComponentBase;

    public sealed record MetaHeaderImageComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] MetaMediaParam[] Parameters
    ) : MetaComponentBase;

    public sealed record MetaHeaderVideoComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] MetaMediaParam[] Parameters
    ) : MetaComponentBase;

    public sealed record MetaHeaderDocumentComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] MetaMediaParam[] Parameters
    ) : MetaComponentBase;

    public sealed record MetaButtonUrlComponent(
        [property: JsonPropertyName("type")] string Type,     // "button"
        [property: JsonPropertyName("sub_type")] string SubType,  // "url"
        [property: JsonPropertyName("index")] string Index,    // NOTE: string for Meta
        [property: JsonPropertyName("parameters")] MetaTextParam[]? Parameters
    ) : MetaComponentBase;
}
