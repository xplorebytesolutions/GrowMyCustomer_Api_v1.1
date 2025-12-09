using System;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class CampaignButton
    {
        public Guid Id { get; set; }

        public Guid CampaignId { get; set; } // 🔗 Foreign key
        public Campaign Campaign { get; set; }

        public string Title { get; set; } = string.Empty; // Button Text (e.g. Buy Now)
        public string Type { get; set; } = "url"; // Type: url, quick_reply, call, etc.
        public string Value { get; set; } = string.Empty; // Target URL or payload

        public int Position { get; set; } // Button order (1–3)
        public bool IsFromTemplate { get; set; } = false;

    }
}
