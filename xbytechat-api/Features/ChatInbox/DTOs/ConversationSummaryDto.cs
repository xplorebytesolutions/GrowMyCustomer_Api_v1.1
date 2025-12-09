using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Read model used by the Chat Inbox to show the left-hand conversation list.
    /// Mirrors the shape used in ChatInbox.jsx (INITIAL_CONVERSATIONS).
    /// </summary>
    public class ConversationSummaryDto
    {
        public Guid ContactId { get; set; }

        /// <summary>
        /// Primary display name (CRM contact name).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// WhatsApp phone number (normalized).
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Latest WhatsApp profile.name we saw.
        /// </summary>
        public string? ProfileName { get; set; }

        /// <summary>
        /// Short preview of the last message in this conversation.
        /// </summary>
        public string? LastMessagePreview { get; set; }

        /// <summary>
        /// When the last message was seen/sent.
        /// </summary>
        public DateTime? LastMessageAt { get; set; }

        /// <summary>
        /// Count of unread inbound messages for this contact.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Priority like "Hot", "Warm", "Cold" (CRM-driven, optional).
        /// </summary>
        public string? Priority { get; set; }

        /// <summary>
        /// True if this contact is treated as VIP or important.
        /// </summary>
        public bool IsVip { get; set; }

        /// <summary>
        /// Conversation mode: "automation" or "agent".
        /// </summary>
        public string Mode { get; set; } = "automation";

        /// <summary>
        /// Name of the assigned agent (if any).
        /// </summary>
        public string? AssignedAgentName { get; set; }

        /// <summary>
        /// CRM tags as chips for quick context (e.g. VIP, Lead, Follow-up).
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Text like "3 notes • 1 reminder today".
        /// </summary>
        public string? LastActivitySummary { get; set; }

        /// <summary>
        /// Text like "Next follow-up tomorrow at 11:30 AM".
        /// </summary>
        public string? TaskSummary { get; set; }
    }
}
