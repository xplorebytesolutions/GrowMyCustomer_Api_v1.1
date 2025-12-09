namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignSummaryDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Delivered { get; set; }
        public int Read { get; set; }

        public string? ImageUrl { get; set; } // ✅ Add this
        public string? ImageCaption { get; set; } // ✅ Add this
        public string? CtaTitle { get; set; } // Optional: For CTA info
        public int RecipientCount { get; set; } // Optional: To show 0/10 etc
    }
}
