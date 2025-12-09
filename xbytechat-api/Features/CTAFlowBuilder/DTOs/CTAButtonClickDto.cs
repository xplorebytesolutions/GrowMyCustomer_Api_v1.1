using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class CTAButtonClickDto
    {
        [Required]
        public string ButtonText { get; set; } = string.Empty;

        [Required]
        public string ButtonType { get; set; } = "cta"; // e.g., "quick_reply", "url", etc.

        [Required]
        public string RecipientNumber { get; set; } = string.Empty;
    }
}
