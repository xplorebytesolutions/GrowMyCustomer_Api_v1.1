namespace xbytechat.api.Features.FlowAnalytics.DTOs
{
    public class MostTriggeredStepDto
    {
        public Guid StepId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int TriggerCount { get; set; }
        public DateTime? LastTriggeredAt { get; set; }
    }

}
