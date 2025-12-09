namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class DecisionNodeConfig
    {
        public string ConditionType { get; set; } = "keyword"; // or "tag", "time", "plan", etc.
        public string Parameter { get; set; } = string.Empty;   // e.g. "yes", "vip", "evening"
        public string SourceChannel { get; set; } = "whatsapp"; // Optional for multi-channel control
    }
}
