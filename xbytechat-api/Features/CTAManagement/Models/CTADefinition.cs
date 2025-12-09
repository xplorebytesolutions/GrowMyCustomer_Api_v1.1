using System;

namespace xbytechat.api.Features.CTAManagement.Models
{
    public class CTADefinition
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; } // 🔗 Business that owns this CTA

        public string Title { get; set; } = string.Empty; // 🏷️ CTA label/title, e.g., "Buy Now"

        public string ButtonText { get; set; } = string.Empty; // 💬 Visible button label

        public string ButtonType { get; set; } = "url"; // 🔘 Options: "url", "quick_reply", etc.

        public string TargetUrl { get; set; } = string.Empty; // 🌐 Action URL or value (depending on type)

        public string Description { get; set; } = string.Empty; // 📝 Optional additional context

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
