namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public class SendTemplateMessageSimpleDto
    {
        public Guid BusinessId { get; set; }
        public string RecipientNumber { get; set; }
        public string TemplateName { get; set; }
        public List<string> TemplateParameters { get; set; } = new();
        // ✅ Add these two for flow tracking
        public Guid? CTAFlowConfigId { get; set; }
        public Guid? CTAFlowStepId { get; set; }
    }
}
