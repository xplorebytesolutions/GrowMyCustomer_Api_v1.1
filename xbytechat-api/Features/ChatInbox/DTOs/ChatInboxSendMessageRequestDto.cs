// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxSendMessageRequestDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Request payload for sending an agent reply from the Chat Inbox.
    /// NOTE: ActorUserId is ALWAYS set by the server from JWT claims.
    /// The client should NOT send it (and we ignore it if they do).
    /// </summary>
    public sealed class ChatInboxSendMessageRequestDto
    {
        public Guid BusinessId { get; set; }

        public string? ConversationId { get; set; }

        public Guid? ContactId { get; set; }

        public string To { get; set; } = string.Empty;

        public string? NumberId { get; set; }

        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Server-side only (claims). Controller will overwrite it.
        /// </summary>
        public Guid ActorUserId { get; set; }
    }
}
