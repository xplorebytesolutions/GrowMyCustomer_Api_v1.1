using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CRM.Timelines.Models
{
    public class LeadTimeline
    {
        public int Id { get; set; }
        public Guid ContactId { get; set; }
        public Guid BusinessId { get; set; }
        public Business Business { get; set; }  // optional

        public Contact Contact { get; set; } // 🆕 Navigation property

        public string EventType { get; set; }

        public string Description { get; set; }
        public string? Data { get; set; }
        public Guid? ReferenceId { get; set; }           // ✅ New
        public bool IsSystemGenerated { get; set; } = false;  // ✅ New
        public string CreatedBy { get; set; }
        public string? Source { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? CTAType { get; set; } // e.g., "BuyNow", "PriceCheck", "ConfirmReminder"
        public string? CTASourceType { get; set; } // e.g., "catalog", "campaign", "reminder"
        public Guid? CTASourceId { get; set; } // ID of the source object (productId, reminderId)

    }
}
