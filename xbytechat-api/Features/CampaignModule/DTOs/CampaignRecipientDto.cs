using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignRecipientDto
    {
        public Guid Id { get; set; }

        public Guid? ContactId { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }

        public string Status { get; set; }
        public DateTime? SentAt { get; set; }

        // 🔁 Advanced Fields (for analytics & future automation)
        public string? BotId { get; set; }
        public string? MessagePreview { get; set; }
        public string? ClickedCTA { get; set; }
        public string? CategoryBrowsed { get; set; }
        public string? ProductBrowsed { get; set; }
        public bool IsAutoTagged { get; set; }
    }
}
