namespace xbytechat.api.Features.FlowAnalytics.DTOs
{
    public class FlowAnalyticsSummaryDto
    {
        public int TotalExecutions { get; set; }
        public int UniqueContacts { get; set; }
        public string TopStepTriggered { get; set; } = "N/A";
        public int TopStepCount { get; set; }
        public DateTime? LastExecutedAt { get; set; }
    }
}

