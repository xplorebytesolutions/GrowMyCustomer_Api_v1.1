// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxSendMessageRequestDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Request payload for sending an agent reply from the Chat Inbox.
    /// This is what the React ChatInbox.jsx will POST.
    /// </summary>
    public sealed class ChatInboxSendMessageRequestDto
    {
        /// <summary>
        /// Tenant/business id (required for multi-tenant isolation).
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Conversation id from the UI. For v1 this is typically ContactId.ToString().
        /// Mainly for tracing; not mandatory for the send logic.
        /// </summary>
        public string? ConversationId { get; set; }

        /// <summary>
        /// CRM Contact id for this chat (preferred for lookups).
        /// </summary>
        public Guid? ContactId { get; set; }

        /// <summary>
        /// Target phone number (normalized WhatsApp number).
        /// Same as selectedConversation.contactPhone in the UI.
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Optional: which WhatsApp number we are sending from
        /// (e.g. "wa-1"). Useful when you support multiple WABA numbers.
        /// </summary>
        public string? NumberId { get; set; }

        /// <summary>
        /// Message body typed by the agent.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}
