using System;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Runtime-only context for executing an AutoReply flow.
    /// This is NOT an EF entity – it will never create a DB table.
    /// </summary>
    public class FlowExecutionContextDto
    {
        /// <summary>
        /// The AutoReply flow being executed (DB entity loaded from AutoReplyFlow table).
        /// </summary>
        public AutoReplyFlow Flow { get; set; } = null!;

        /// <summary>
        /// Business (tenant) ID for scoping.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Contact ID in your CRM / Contacts table.
        /// </summary>
        public Guid ContactId { get; set; }

        /// <summary>
        /// WhatsApp phone number (MSISDN) of the contact.
        /// </summary>
        public string ContactPhone { get; set; } = null!;

        /// <summary>
        /// Channel source, e.g. "whatsapp". Kept flexible for future multi-channel.
        /// </summary>
        public string SourceChannel { get; set; } = "whatsapp";

        /// <summary>
        /// Optional industry tag (restaurant, clinic, etc.) for analytics / specialization.
        /// </summary>
        public string IndustryTag { get; set; } = string.Empty;
    }
}
