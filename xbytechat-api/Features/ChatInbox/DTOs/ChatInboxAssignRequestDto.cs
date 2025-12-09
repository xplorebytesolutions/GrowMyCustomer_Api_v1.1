// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxAssignRequestDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Request payload for assigning a conversation (contact) to a specific user.
    /// </summary>
    public sealed class ChatInboxAssignRequestDto
    {
        /// <summary>
        /// Tenant/business id.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Contact id representing the conversation.
        /// </summary>
        public Guid ContactId { get; set; }

        /// <summary>
        /// Agent/user who will own this conversation.
        /// </summary>
        public Guid UserId { get; set; }
    }

    /// <summary>
    /// Request payload for unassigning a conversation.
    /// </summary>
    public sealed class ChatInboxUnassignRequestDto
    {
        /// <summary>
        /// Tenant/business id.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Contact id representing the conversation.
        /// </summary>
        public Guid ContactId { get; set; }
    }
}
