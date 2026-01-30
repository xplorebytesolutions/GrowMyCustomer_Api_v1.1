#nullable enable
using System;

namespace xbytechat.api.Features.ESU.Facebook.DTOs
{
    public sealed class EsuStatusDto
    {
        public bool Connected { get; init; }             // true = ESU-complete + valid token
        public bool HasEsuFlag { get; init; }            // IntegrationFlags row + FacebookEsuCompleted
        public bool HasValidToken { get; init; }         // from TryGetValidAsync
        public DateTime? TokenExpiresAtUtc { get; init; }
        public bool WillExpireSoon { get; init; }
        public bool HardDeleted { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
        public string? Debug { get; init; }
    }

}
