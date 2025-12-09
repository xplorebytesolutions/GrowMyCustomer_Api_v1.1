using System.Text.Json.Serialization;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Pinnacle;

namespace xbytechat.api.Infrastructure.Json
{
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(MetaTemplateMessage))]
    [JsonSerializable(typeof(PinnacleTemplateMessage))]
    // Ensure the generator knows about the polymorphic bases
    [JsonSerializable(typeof(System.Collections.Generic.List<MetaComponentBase>))]
    [JsonSerializable(typeof(System.Collections.Generic.List<PinnacleComponentBase>))]

    public partial class JsonCtx : JsonSerializerContext { }
}
