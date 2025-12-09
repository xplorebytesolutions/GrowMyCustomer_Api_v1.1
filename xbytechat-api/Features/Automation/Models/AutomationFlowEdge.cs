namespace xbytechat.api.Features.Automation.Models
{
    public class AutomationFlowEdge
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string? Condition { get; set; }
    }
}
