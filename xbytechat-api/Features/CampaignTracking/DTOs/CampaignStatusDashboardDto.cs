namespace xbytechat.api.Features.CampaignTracking.DTOs
{
    public class CampaignStatusDashboardDto
    {
        public Guid CampaignId { get; set; }

        // 📊 Overall Stats
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int DeliveredCount { get; set; }
        public int ReadCount { get; set; }
        public int FailedCount { get; set; }

        // 🕒 Delivery Timing (optional but insightful)
        public DateTime? FirstSentAt { get; set; }
        public DateTime? LastSentAt { get; set; }
        public DateTime? FirstReadAt { get; set; }
        public DateTime? LastReadAt { get; set; }

        // 📉 Delivery Rates
        public double DeliveryRate => TotalRecipients == 0 ? 0 : (double)DeliveredCount / TotalRecipients * 100;
        public double ReadRate => TotalRecipients == 0 ? 0 : (double)ReadCount / TotalRecipients * 100;
        public double FailureRate => TotalRecipients == 0 ? 0 : (double)FailedCount / TotalRecipients * 100;
    }
}
