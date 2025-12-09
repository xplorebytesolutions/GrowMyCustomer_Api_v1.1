namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class VisualFlowLoadDto
    {
        public string FlowName { get; set; } = string.Empty;
        public List<FlowNodeDto> Nodes { get; set; } = new();
        public List<FlowEdgeDto> Edges { get; set; } = new();
    }
}
