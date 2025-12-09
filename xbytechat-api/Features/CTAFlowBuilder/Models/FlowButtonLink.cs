namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    public class FlowButtonLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ButtonText { get; set; } = string.Empty;
        public Guid? NextStepId { get; set; }
        // ✅ NEW FIELDS FOR FUTURE AUTOMATION
        public string ButtonType { get; set; } = "QUICK_REPLY";    // e.g., URL, QUICK_REPLY, FLOW
        public string ButtonSubType { get; set; } = "";            // Optional: e.g., "Catalog", "PricingCTA"
        public string ButtonValue { get; set; } = "";              // e.g., URL or deep link

        // Optional FK back to Step if needed
        public Guid CTAFlowStepId { get; set; }
        public CTAFlowStep? Step { get; set; }

        public short ButtonIndex { get; set; }
    }
}
