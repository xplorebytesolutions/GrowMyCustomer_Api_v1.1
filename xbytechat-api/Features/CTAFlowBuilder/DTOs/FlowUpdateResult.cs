namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public sealed class FlowUpdateResult
    {
        // ok | requiresFork | notFound | error
        public string Status { get; set; } = "ok";
        public string? Message { get; set; }
        public bool NeedsRepublish { get; set; } // true when we flipped published->draft to allow editing
        public object? Campaigns { get; set; }   // list for UI when requiresFork
    }
}
