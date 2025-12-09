namespace xbytechat.api.Features.AutoReplyBuilder.Flows.DTOs.FlowNodeConfigs
{
    public class TemplateConfig
    {
        public string TemplateName { get; set; } = string.Empty;
        public List<string> Placeholders { get; set; } = new();
        public string? Language { get; set; } = "en_US";
        public string? ImageUrl { get; set; }
        public List<TemplateButtonDto>? MultiButtons { get; set; } = new();
    }

    public class TemplateButtonDto
    {
        public string ButtonText { get; set; } = string.Empty;
        public string ButtonType { get; set; } = "url"; // or "quick_reply"
        public string TargetUrl { get; set; } = string.Empty;
    }
}
