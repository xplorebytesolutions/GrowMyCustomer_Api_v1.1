// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxMarkReadRequestDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Request payload for marking a conversation as "read"
    /// in the Chat Inbox.
    /// </summary>
    public sealed class ChatInboxMarkReadRequestDto
    {
        /// <summary>
        /// Tenant/business id (required for multi-tenant isolation).
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// CRM Contact id whose messages are being marked as read.
        /// </summary>
        public Guid ContactId { get; set; }

        /// <summary>
        /// Optional timestamp for "last read". If not supplied,
        /// the server will use DateTime.UtcNow.
        /// </summary>
        public DateTime? LastReadAtUtc { get; set; }
    }
}
