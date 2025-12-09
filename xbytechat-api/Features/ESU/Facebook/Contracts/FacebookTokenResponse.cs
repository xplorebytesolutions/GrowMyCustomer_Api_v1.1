#nullable enable
using System.Text.Json.Serialization;

namespace xbytechat.api.Features.ESU.Facebook.Contracts
{
    public sealed class FacebookTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = "bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; }

        /// <summary>Raw JSON payload as returned by Facebook for auditing/debugging.</summary>
        public string RawJson { get; init; } = string.Empty;
    }
}
