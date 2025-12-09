namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class CTAFlowExecutionRequest
    {
        public string ButtonText { get; set; } = string.Empty;
        public string ButtonType { get; set; } = "cta";
        public string RecipientNumber { get; set; } = string.Empty;
    }
}
