namespace xbytechat.api.Features.Catalog.DTOs
{
    public class CatalogClickLogDto
    {
        public Guid BusinessId { get; set; }
        public Guid? ContactId { get; set; }  // ✅ Add this
        public Guid? ProductId { get; set; }

        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }

        public string? BotId { get; set; }
        public string? CategoryBrowsed { get; set; }
        public string? ProductBrowsed { get; set; }
        public string? CTAJourney { get; set; }
        public Guid? MessageLogId { get; set; }      // ✅ ADD THIS FIELD
        public string? RefMessageId { get; set; }

        public string TemplateId { get; set; }
        public string ButtonText { get; set; }

        // 🧠 Optional: Used for A/B tracking and analytics
        public string? PlanSnapshot { get; set; }

        public string? Source { get; set; } // ✅ Add this
    }
}
