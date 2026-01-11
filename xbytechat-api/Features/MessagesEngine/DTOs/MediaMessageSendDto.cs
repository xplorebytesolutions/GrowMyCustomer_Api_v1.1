using System;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public sealed class MediaMessageSendDto
    {
        public Guid BusinessId { get; set; }

        public string RecipientNumber { get; set; } = string.Empty;

        /// <summary>
        /// WhatsApp Cloud API media_id (returned by /{phone_number_id}/media).
        /// </summary>
        public string MediaId { get; set; } = string.Empty;

        /// <summary>
        /// Optional caption.
        /// </summary>
        public string? Caption { get; set; }

        /// <summary>
        /// Optional filename (recommended for documents).
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Optional mime type for logging/display.
        /// </summary>
        public string? MimeType { get; set; }

        public Guid ContactId { get; set; }

        public string? PhoneNumberId { get; set; }

        public string? Provider { get; set; }

        public string? Source { get; set; }
    }
}

