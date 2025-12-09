// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxFilterDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Filters used by the Chat Inbox conversation list endpoint.
    /// This matches the UI needs: tab, number, search, "my" vs "unassigned".
    /// </summary>
    public sealed class ChatInboxFilterDto
    {
        /// <summary>
        /// Business Id (tenant). Mandatory for multi-tenant isolation.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Currently logged-in user id (for "my" filter).
        /// Optional: if null, "my" filter is ignored.
        /// </summary>
        public Guid? CurrentUserId { get; set; }

        /// <summary>
        /// "live" | "history" | "unassigned" | "my"
        /// </summary>
        public string? Tab { get; set; }

        /// <summary>
        /// WhatsApp number id, e.g. "wa-num-1". If null or "all", no filter.
        /// </summary>
        public string? NumberId { get; set; }

        /// <summary>
        /// Free text search over name, phone, and last message preview.
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Max number of conversations to return. Hard-capped to 200.
        /// </summary>
        public int Limit { get; set; } = 50;

        /// <summary>
        /// If true: only conversations without AssignedToUserId.
        /// </summary>
        public bool OnlyUnassigned { get; set; }

        /// <summary>
        /// If true: only conversations assigned to CurrentUserId.
        /// </summary>
        public bool OnlyAssignedToMe { get; set; }
    }
}
