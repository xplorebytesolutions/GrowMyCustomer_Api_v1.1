namespace xbytechat.api.Features.Catalog.Models
{
    public class CatalogClickLog
    {
        public Guid Id { get; set; }

        // 🔗 Business & Product Info
        public Guid BusinessId { get; set; }
        public Guid? ProductId { get; set; }

        // 👤 Customer Info
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }

        // 🤖 Bot / Messaging Context
        public string? BotId { get; set; }

        // 🛍️ Browsing Context
        public string? CategoryBrowsed { get; set; }
        public string? ProductBrowsed { get; set; }

        // 🔘 CTA Clicked
        public string? CTAJourney { get; set; }
        public string TemplateId { get; set; }
        public string RefMessageId { get; set; }
        public string ButtonText { get; set; }

        // 🕒 Meta
        public DateTime? ClickedAt { get; set; } = DateTime.UtcNow;

        // ✅ CRM / Campaign / Analytics
        public Guid? CampaignSendLogId { get; set; }
        public Guid? ContactId { get; set; }
        public bool FollowUpSent { get; set; } = false;
        public string? LastInteractionType { get; set; }
        public Guid? MessageLogId { get; set; }
        public string? PlanSnapshot { get; set; }

        // 🆕 [New Additions for CTA Campaign Tracking]
        public Guid? CtaId { get; set; }             // Link to CTA definition
        public Guid? CampaignId { get; set; }        // Link to campaign (if any)
        public string Source { get; set; } = "catalog"; // "catalog", "campaign", "auto-reply"
    }
}
