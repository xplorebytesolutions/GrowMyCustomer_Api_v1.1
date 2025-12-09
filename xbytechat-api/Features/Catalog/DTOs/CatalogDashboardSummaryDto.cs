namespace xbytechat.api.Features.Catalog.DTOs
{
    public class CatalogDashboardSummaryDto
    {
        // Engagement Metrics
        public int TotalMessagesSent { get; set; }
        public int? UniqueCustomersMessaged { get; set; }
        public int? ProductClicks { get; set; }

        // Catalog Overview
        public int? ActiveProducts { get; set; }
        public int? ProductsSharedViaWhatsApp { get; set; }

        // Lead Intelligence
        public int? RepeatClickers { get; set; }
        public int? NewClickersToday { get; set; }

        // Timestamps
        public DateTime? LastCatalogClickAt { get; set; }
        public DateTime? LastMessageSentAt { get; set; }
    }
}