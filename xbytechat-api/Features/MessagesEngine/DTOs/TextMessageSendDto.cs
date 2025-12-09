using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public class TextMessageSendDto
    {
        public Guid BusinessId { get; set; }

        public string RecipientNumber { get; set; }

        public string TextContent { get; set; }

        public Guid ContactId { get; set; }

        public string? PhoneNumberId { get; set; }
        // ✅ NEW: Optional source indicator (e.g., "campaign", "auto-reply", etc.)

        //[RegularExpression("^(PINNACLE|META_CLOUD)$")]
        //[Required]
        public string Provider { get; set; } = string.Empty;
        public string? Source { get; set; }

        // ✅ NEW: Optional message ID for campaign tracing
        public string? MessageId { get; set; }

        public bool IsSaveContact { get; set; } = false; // default true
    }
}
