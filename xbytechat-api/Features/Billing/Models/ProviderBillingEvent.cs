using System;

namespace xbytechat_api.Features.Billing.Models
{
    public class ProviderBillingEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BusinessId { get; set; }

        // Link if we can; may be null if webhook arrives before we create MessageLog
        public Guid? MessageLogId { get; set; }

        public string Provider { get; set; } = "";          // "Meta_cloud", "Pinnacle"
        public string EventType { get; set; } = "";         // "conversation_started", "message_delivered", "pricing_update", etc.

        public string? ProviderMessageId { get; set; }      // "wamid..."
        public string? ConversationId { get; set; }
        public string? ConversationCategory { get; set; }
        public bool? IsChargeable { get; set; }
        public decimal? PriceAmount { get; set; }
        public string? PriceCurrency { get; set; }

        public string PayloadJson { get; set; } = "";       // original provider payload for audit
        public DateTimeOffset OccurredAt { get; set; }      // when provider says it happened
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
