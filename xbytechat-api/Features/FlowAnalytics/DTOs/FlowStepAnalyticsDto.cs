namespace xbytechat.api.Features.FlowAnalytics.DTOs
{
    public class FlowStepAnalyticsDto
    {
        public Guid StepId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public int TotalReached { get; set; }
        public int ClickedNext { get; set; }
        public int DropOff => TotalReached - ClickedNext;
        public Guid? NextStepId { get; set; }
        public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
