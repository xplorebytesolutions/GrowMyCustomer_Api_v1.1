namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class VisualFlowSummaryDto
    {
        public Guid Id { get; set; }
        public string FlowName { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
