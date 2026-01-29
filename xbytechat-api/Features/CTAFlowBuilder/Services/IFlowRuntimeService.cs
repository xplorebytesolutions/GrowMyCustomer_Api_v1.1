using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    public interface IFlowRuntimeService
    {
        Task<NextStepResult> ExecuteNextAsync(NextStepContext context);

    }
    public record NextStepContext
    {
        public Guid BusinessId { get; set; }
        public Guid FlowId { get; set; }
        public int Version { get; set; }
        public Guid SourceStepId { get; set; }
        public Guid? TargetStepId { get; set; }
        public short ButtonIndex { get; set; }
        public Guid MessageLogId { get; set; }
        public string ContactPhone { get; set; } = string.Empty;
        public Guid RequestId { get; set; }
        public FlowButtonLink? ClickedButton { get; set; }
        public string? Provider { get; set; }          // "META_CLOUD" | "PINNACLE"
        public string? PhoneNumberId { get; set; }
            public bool AlwaysSend { get; set; } = true;

        // CTA media-header support (step 1/phase 1):
        // When a template requires a media header (image/video/document), WhatsApp requires a header component with a link.
        // In phase 2/3 we will persist this per-step (CTAFlowStep.HeaderMediaUrl) via UI + DB.
        // For now this is an optional override input so runtime can attach the header when available.
        public string? HeaderMediaUrl { get; set; }
    }

    public record NextStepResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
