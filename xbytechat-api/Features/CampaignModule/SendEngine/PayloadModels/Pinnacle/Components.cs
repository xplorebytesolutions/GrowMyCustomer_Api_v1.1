using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Pinnacle
{
    // Pinnacle differences: "subType" camelCase; "index" is NUMBER.

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$pinnType")]
    [JsonDerivedType(typeof(PinnBodyComponent), typeDiscriminator: "body")]
    [JsonDerivedType(typeof(PinnHeaderTextComponent), typeDiscriminator: "header_text")]
    [JsonDerivedType(typeof(PinnHeaderImageComponent), typeDiscriminator: "header_image")]
    [JsonDerivedType(typeof(PinnHeaderVideoComponent), typeDiscriminator: "header_video")]
    [JsonDerivedType(typeof(PinnHeaderDocumentComponent), typeDiscriminator: "header_document")]
    [JsonDerivedType(typeof(PinnButtonUrlComponent), typeDiscriminator: "button_url")]
    public abstract record PinnacleComponentBase;

    public sealed record PinnTextParam(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text
    );

    public sealed record PinnMediaParam(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("image")] PinnMediaLink? Image = null,
        [property: JsonPropertyName("video")] PinnMediaLink? Video = null,
        [property: JsonPropertyName("document")] PinnMediaLink? Document = null
    );

    public sealed record PinnMediaLink([property: JsonPropertyName("link")] string Link);

    public sealed record PinnBodyComponent(
        [property: JsonPropertyName("type")] string Type, // "body"
        [property: JsonPropertyName("parameters")] PinnTextParam[] Parameters
    ) : PinnacleComponentBase;

    public sealed record PinnHeaderTextComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] PinnTextParam[] Parameters
    ) : PinnacleComponentBase;

    public sealed record PinnHeaderImageComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] PinnMediaParam[] Parameters
    ) : PinnacleComponentBase;

    public sealed record PinnHeaderVideoComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] PinnMediaParam[] Parameters
    ) : PinnacleComponentBase;

    public sealed record PinnHeaderDocumentComponent(
        [property: JsonPropertyName("type")] string Type, // "header"
        [property: JsonPropertyName("parameters")] PinnMediaParam[] Parameters
    ) : PinnacleComponentBase;

    public sealed record PinnButtonUrlComponent(
        [property: JsonPropertyName("type")] string Type,     // "button"
        [property: JsonPropertyName("subType")] string SubType,  // "url"
        [property: JsonPropertyName("index")] int Index,       // NOTE: number for Pinnacle
        [property: JsonPropertyName("parameters")] PinnTextParam[]? Parameters
    ) : PinnacleComponentBase;
}
