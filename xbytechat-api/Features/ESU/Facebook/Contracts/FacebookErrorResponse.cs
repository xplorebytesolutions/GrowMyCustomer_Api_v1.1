#nullable enable
using System.Text.Json.Serialization;

namespace xbytechat.api.Features.ESU.Facebook.Contracts
{
    public sealed class FacebookErrorResponse
    {
        [JsonPropertyName("error")]
        public FacebookError? Error { get; init; }

        public sealed class FacebookError
        {
            [JsonPropertyName("message")] public string? Message { get; init; }
            [JsonPropertyName("type")] public string? Type { get; init; }
            [JsonPropertyName("code")] public int? Code { get; init; }
            [JsonPropertyName("error_subcode")] public int? SubCode { get; init; }
            [JsonPropertyName("fbtrace_id")] public string? TraceId { get; init; }
        }
    }
}
