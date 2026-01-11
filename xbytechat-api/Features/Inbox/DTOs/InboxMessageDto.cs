// 📄 File: Features/Inbox/DTOs/InboxMessageDto.cs
using System;

namespace xbytechat.api.Features.Inbox.DTOs
{
    public class InboxMessageDto
    {
        public Guid BusinessId { get; set; }
        public string RecipientPhone { get; set; }
        public string MessageBody { get; set; }
        public Guid? ContactId { get; set; }
        public Guid? CTAFlowStepId { get; set; }
        public Guid? CTAFlowConfigId { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }       // 🆕 To show in chat bubble
        public string? RenderedBody { get; set; }

        public bool IsIncoming { get; set; }            // 🆕 Needed for bubble side
        public string Status { get; set; }              // 🆕 For message ticks
        public DateTime SentAt { get; set; }            // 🆕 For timestamp

        // ✅ NEW: Provider/WAMID idempotency key (Meta "messages[0].id")
        public string? ProviderMessageId { get; set; }

        // ✅ NEW: WhatsApp native media (for inbound/outbound attachments)
        // Stored as media_id (not public URL) to avoid hosting files ourselves.
        public string? MediaId { get; set; }            // WhatsApp Cloud API media_id
        public string? MediaType { get; set; }          // "image" | "document" | "video" | "audio" | "location"
        public string? FileName { get; set; }           // original filename (if available)
        public string? MimeType { get; set; }           // e.g. "image/jpeg", "application/pdf"

        // ?? WhatsApp location message fields (no MediaId)
        public double? LocationLatitude { get; set; }
        public double? LocationLongitude { get; set; }
        public string? LocationName { get; set; }
        public string? LocationAddress { get; set; }
    }
}
