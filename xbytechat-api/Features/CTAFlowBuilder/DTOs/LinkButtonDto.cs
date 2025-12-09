namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class LinkButtonDto
    {
        public string Text { get; set; } = string.Empty;
        public int Index { get; set; } = -1;
        public string? Type { get; set; } // 🔥 e.g., "URL", "QUICK_REPLY"
        public string? SubType { get; set; } // 🔥 e.g., "STATIC", "DYNAMIC"
        public string? Value { get; set; } // 🔥 the parameter or url or payload

        public string? TargetNodeId { get; set; } // 🔄 used for flow linking
    }
}
