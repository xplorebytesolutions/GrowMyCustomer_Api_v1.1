using System;

namespace xbytechat.api.Features.CampaignTracking.DTOs
{
    public class CampaignContactListItemDto
    {
        // ✅ Needed for Phase-2 actions (retry/delete/redirect selected)
        public Guid SendLogId { get; set; }

        // ✅ Useful for drill-down / audits / inbox links
        public Guid? MessageLogId { get; set; }
        public Guid? RecipientId { get; set; }

        public Guid? ContactId { get; set; }
        public string ContactName { get; set; } = "N/A";
        public string ContactPhone { get; set; } = "-";
        public string? RecipientNumber { get; set; }

        public string? SendStatus { get; set; }
        public string? ErrorMessage { get; set; }

        public bool IsClicked { get; set; }
        public string? ClickType { get; set; }
        public DateTime? ClickedAt { get; set; }

        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        // For replied bucket
        public DateTime? LastInboundAt { get; set; }

        // ✅ One ordering timestamp for UI
        public DateTime? LastUpdatedAt { get; set; }
    }
}
