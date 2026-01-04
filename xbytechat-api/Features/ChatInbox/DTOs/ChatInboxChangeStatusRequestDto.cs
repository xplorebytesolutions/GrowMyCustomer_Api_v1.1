// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxChangeStatusRequestDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Command payload for changing the status of an Inbox conversation.
    /// Internally this maps to Contact.IsArchived / IsActive.
    /// </summary>
    public sealed class ChatInboxChangeStatusRequestDto
    {
        /// <summary>
        /// Tenant / business id (required).
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Contact whose conversation we want to change (required).
        /// </summary>
        public Guid ContactId { get; set; }

        /// <summary>
        /// Target status: "Open" | "Closed" (case-insensitive).
        /// We also accept "New"/"Pending" but treat them as Open internally.
        /// </summary>
        public string? TargetStatus { get; set; }

        // Compatibility alias for newer clients posting { status: "Open"|"Pending"|"Closed" }
        public string? Status { get; set; }
    }
}
