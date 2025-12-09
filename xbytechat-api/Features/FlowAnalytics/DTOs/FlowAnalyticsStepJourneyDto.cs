namespace xbytechat.api.Features.FlowAnalytics.DTOs
{
    public class FlowAnalyticsStepJourneyDto
    {
        public Guid StepId { get; set; }

        public string TemplateName { get; set; } = string.Empty;

        public int TotalReached { get; set; }

        public int ClickedNext { get; set; }

        public Guid? NextStepId { get; set; }

        // ✅ Auto-calculated: number of users who dropped off at this step
        public int DropOff => TotalReached - ClickedNext;

        // ✅ Auto-calculated: percentage of users who clicked "next"
        public double ConversionRate =>
            TotalReached == 0 ? 0 : Math.Round((double)ClickedNext / TotalReached * 100, 2);

        // ✅ Optional: percentage of users who dropped off
        public double DropOffRate =>
            TotalReached == 0 ? 0 : Math.Round((double)DropOff / TotalReached * 100, 2);
    }
}
