using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Minimal request for testing runtime auto-reply flow matching.
    /// </summary>
    public sealed class AutoReplyMatchRequestDto
    {
        public Guid BusinessId { get; set; }
        public string IncomingText { get; set; } = string.Empty;
    }
}
