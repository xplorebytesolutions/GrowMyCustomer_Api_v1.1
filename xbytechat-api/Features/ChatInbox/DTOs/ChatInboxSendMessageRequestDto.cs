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

        /// <summary>
        /// Optional for media messages. Required when sending plain text.
        /// For media, this becomes the optional caption.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// WhatsApp Cloud API media id returned by /{phone_number_id}/media upload.
        /// </summary>
        public string? MediaId { get; set; }

        /// <summary>
        /// "image" | "document" | "video" | "audio"
        /// For location messages, send coordinates and set MediaType to "location" (MediaId must be null).
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Original filename for UI display (optional).
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Client-supplied mime type for display/logging (optional).
        /// </summary>
        public string? MimeType { get; set; }

        /// <summary>
        /// Optional: WhatsApp "location" message fields.
        /// When provided, MediaId must be null.
        /// </summary>
        public double? LocationLatitude { get; set; }
        public double? LocationLongitude { get; set; }
        public string? LocationName { get; set; }
        public string? LocationAddress { get; set; }

        /// <summary>
        /// Server-side only (claims). Controller will overwrite it.
        /// </summary>
        public Guid ActorUserId { get; set; }
    }
}
