// 📄 File: xbytechat.api/Features/CTAFlowBuilder/DTOs/CreateFlowDto.cs
namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class CreateFlowDto
    {
        public string FlowName { get; set; } = string.Empty;
        public List<FlowStepDto> Steps { get; set; } = new();
        public bool IsPublished { get; set; } = false; // ✅ NEW: Draft vs Published
    }

    public class FlowStepDto
    {
        public string TriggerButtonText { get; set; } = string.Empty;
        public string TriggerButtonType { get; set; } = string.Empty;
        public string TemplateToSend { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public List<ButtonLinkDto> ButtonLinks { get; set; } = new();
    }

    public class ButtonLinkDto
    {
        public string ButtonText { get; set; } = string.Empty;
        public Guid NextStepId { get; set; }
    }
}
