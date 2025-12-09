namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class FlowEdgeDto
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string? SourceHandle { get; set; }
    }
}
