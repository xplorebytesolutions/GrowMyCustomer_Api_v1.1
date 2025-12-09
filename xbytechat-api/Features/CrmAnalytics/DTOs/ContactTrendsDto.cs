namespace xbytechat.api.Features.CrmAnalytics.DTOs
{
    /// <summary>
    /// Represents the number of contacts added on a specific date.
    /// Used for trend charting on the CRM dashboard.
    /// </summary>
    public class ContactTrendsDto
    {
        public string Date { get; set; } // Format: yyyy-MM-dd
        public int Count { get; set; }
    }
}
