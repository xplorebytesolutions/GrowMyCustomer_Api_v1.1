using System;

namespace xbytechat.api.Features.ESU.Facebook.DTOs
{
    // Request from FE to start ESU. BusinessId now comes from header X-Business-Id.
    public sealed class FacebookEsuStartRequestDto
    {
        public string? ReturnUrlAfterSuccess { get; set; }   // optional FE page to navigate to after success
    }

    // Service-layer response; controller will wrap this into the envelope.
    public sealed class FacebookEsuStartResponseDto
    {
        public string LaunchUrl { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;    // returned for debugging/telemetry if needed
        public DateTime ExpiresAtUtc { get; set; }           // when the state will expire on server
    }

    public sealed class FacebookEsuCallbackResponseDto
    {
        public string RedirectTo { get; set; } = "/esu/success";
    }
}
