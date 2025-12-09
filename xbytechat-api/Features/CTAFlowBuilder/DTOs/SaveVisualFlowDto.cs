namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class SaveVisualFlowDto
    {
        public string FlowName { get; set; } = string.Empty;
        public bool IsPublished { get; set; }

        public List<FlowNodeDto> Nodes { get; set; } = new();
        public List<FlowEdgeDto> Edges { get; set; } = new();
        public Guid? CampaignId { get; set; } // ✅ Add this line
    }
}
