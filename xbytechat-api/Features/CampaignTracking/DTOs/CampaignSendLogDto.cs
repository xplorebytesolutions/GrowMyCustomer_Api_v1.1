using System;

namespace xbytechat.api.Features.CampaignTracking.DTOs
{
    public class CampaignSendLogDto
    {
        public Guid Id { get; set; }

        // 🔗 Relationships
        public Guid CampaignId { get; set; }
        public Guid? ContactId { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }

        public string? RecipientNumber { get; set; }
        // 📤 Message Info
        public Guid RecipientId { get; set; }
        public string MessageBody { get; set; }
        public string? TemplateId { get; set; }
        public string? SendStatus { get; set; }
        public string? ErrorMessage { get; set; }

        // 🕒 Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        // 🌐 Metadata
        public string? SourceChannel { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
        public string? MacAddress { get; set; }

        // ✅ Enriched metadata
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }

        // 📈 Click Tracking
        public bool IsClicked { get; set; }
        public DateTime? ClickedAt { get; set; }
        public string? ClickType { get; set; }

        // 🔁 Retry Info
        public string? RetryStatus { get; set; }     // Pending, Retried, Skipped
        public int RetryCount { get; set; }
        public DateTime? LastRetryAt { get; set; }
    }
}
