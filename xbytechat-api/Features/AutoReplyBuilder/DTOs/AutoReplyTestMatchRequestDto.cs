using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Request body for POST /api/autoreplyflows/test-match.
    /// Sent by the AutoReplyBuilder UI when testing a sample message.
    /// </summary>
    public sealed class AutoReplyTestMatchRequestDto
    {
        /// <summary>
        /// Business (tenant) ID – taken from the logged-in user's auth context in the UI.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Sample incoming text typed by the user in the "Test Auto-Reply Match" panel.
        /// </summary>
        public string IncomingText { get; set; } = string.Empty;
    }
}
