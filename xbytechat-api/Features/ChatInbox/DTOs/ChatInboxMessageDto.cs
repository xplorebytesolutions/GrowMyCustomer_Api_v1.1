// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxMessageDto.cs
using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Single message in a Chat Inbox conversation.
    /// Kept intentionally simple and stable so the React UI
    /// can bind to it without worrying about provider details.
    /// </summary>
    public sealed class ChatInboxMessageDto
    {
        public Guid Id { get; set; }

        /// <summary>
        /// "in"  = message came from customer to us.
        /// "out" = message we sent to customer.
        /// For now we only have reliable data for "out"; we keep
        /// the string type so we can extend it later without schema changes.
        /// </summary>
        public string Direction { get; set; } = "out";

        /// <summary>
        /// Channel identifier (e.g. "whatsapp") – future-proofing.
        /// </summary>
        public string Channel { get; set; } = "whatsapp";

        /// <summary>
        /// Rendered text content for the bubble.
        /// For templates we'll store the final rendered body.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// WhatsApp media_id (if this is a media message).
        /// </summary>
        public string? MediaId { get; set; }

        /// <summary>
        /// "image" | "document" | "video" | "audio" | "location"
        /// </summary>
        public string? MediaType { get; set; }

        public string? FileName { get; set; }
        public string? MimeType { get; set; }

        // Location message fields (when MediaType == "location")
        public double? LocationLatitude { get; set; }
        public double? LocationLongitude { get; set; }
        public string? LocationName { get; set; }
        public string? LocationAddress { get; set; }

        /// <summary>
        /// When we created/sent the message (UTC).
        /// If SentAt is missing, falls back to CreatedAt.
        /// </summary>
        public DateTime SentAtUtc { get; set; }

        /// <summary>
        /// Provider / delivery status (“Sent”, “Delivered”, “Read”, “Failed”, etc.),
        /// mapped from MessageLog.Status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// For failed messages, a short error string from MessageLog.ErrorMessage.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
