namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public sealed class CtaFlowRunResult
    {
        /// <summary>
        /// True if the CTA flow was started/executed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Optional human-readable error message when Success = false.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
