// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxConversationDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Lightweight conversation summary for the Chat Inbox UI.
    /// Mirrors the front-end ConversationSummary model.
    /// </summary>
    public sealed class ChatInboxConversationDto
    {
        /// <summary>
        /// Conversation identifier for the UI.
        /// For v1 this can be derived from (ContactId + NumberId).
        /// In the future, if you create an InboxConversation table,
        /// use its primary key here.
        /// </summary>
        public string Id { get; set; } = default!;

        public Guid ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;

        public string LastMessagePreview { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }

        public int UnreadCount { get; set; }

        /// <summary>
        /// "New" | "Open" | "Pending" | "Closed"
        /// </summary>
        public string Status { get; set; } = "Open";

        /// <summary>
        /// WhatsApp number id (e.g. wa-num-1).
        /// Later you may map this to WhatsAppPhoneNumber.Id.
        /// </summary>
        public string NumberId { get; set; } = string.Empty;

        public string NumberLabel { get; set; } = string.Empty;

        /// <summary>
        /// True if within 24h messaging window (WhatsApp session).
        /// </summary>
        public bool Within24h { get; set; }

        public string? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public bool IsAssignedToMe { get; set; }

        /// <summary>
        /// "automation" | "agent"
        /// </summary>
        public string Mode { get; set; } = "automation";

        /// <summary>
        /// "AutoReply" | "Campaign" | "Manual" | "Unknown"
        /// </summary>
        public string SourceType { get; set; } = "Unknown";

        /// <summary>
        /// Campaign name / AutoReply flow name / other source label.
        /// </summary>
        public string? SourceName { get; set; }

        public DateTime? FirstSeenAt { get; set; }
        public DateTime? LastInboundAt { get; set; }
        public DateTime? LastOutboundAt { get; set; }
        public int TotalMessages { get; set; }

        public DateTime? LastAgentReplyAt { get; set; }
        public DateTime? LastAutomationAt { get; set; }
    }
}
