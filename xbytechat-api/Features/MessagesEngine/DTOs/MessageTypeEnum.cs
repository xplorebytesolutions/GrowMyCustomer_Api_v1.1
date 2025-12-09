using System.Text.Json.Serialization;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    [JsonConverter(typeof(JsonStringEnumConverter))] // Enables string parsing in JSON
    public enum MessageTypeEnum
    {
        Text,
        Image,
        Template,
        Cta
    }
}
