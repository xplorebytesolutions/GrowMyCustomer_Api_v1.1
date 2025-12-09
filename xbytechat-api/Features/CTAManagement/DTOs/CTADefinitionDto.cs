namespace xbytechat.api.Features.CTAManagement.DTOs
{
    public class CTADefinitionDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty; // 🏷️ CTA label (e.g., "Buy Now")

        public string ButtonText { get; set; } = string.Empty; // 💬 Visible button label (e.g., "Buy Now")

        public string ButtonType { get; set; } = "url"; // 🔘 Expected values: "url", "quick_reply", etc.

        public string TargetUrl { get; set; } = string.Empty; // 🌐 Redirect or action target

        public string? Description { get; set; } // 📝 Optional description (for context/tooltip)

        public bool IsActive { get; set; } = true;
    }
}
