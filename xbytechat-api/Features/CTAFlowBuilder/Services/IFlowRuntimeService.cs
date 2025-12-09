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
    }

    public record NextStepResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
