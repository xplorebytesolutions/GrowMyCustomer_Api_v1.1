namespace xbytechat.api.Features.MessageManagement.DTOs
{
    public class WebhookStatusDto
    {
        public List<StatusObject> statuses { get; set; }
    }

    public class StatusObject
    {
        public string id { get; set; }                     // Message ID (WAMID)
        public string status { get; set; }                 // sent, delivered, read, failed
        public long timestamp { get; set; }                // UNIX timestamp
        public string recipient_id { get; set; }           // Phone number
        public ConversationInfo conversation { get; set; }
        public PricingInfo pricing { get; set; }
        public List<ErrorInfo>? errors { get; set; }
    }

    public class ConversationInfo
    {
        public string id { get; set; }
        public Origin origin { get; set; }
    }

    public class Origin
    {
        public string type { get; set; }                   // marketing, utility, etc.
    }

    public class PricingInfo
    {
        public bool billable { get; set; }
        public string pricing_model { get; set; }
        public string category { get; set; }               // Template category
    }

    public class ErrorInfo
    {
        public int code { get; set; }
        public string title { get; set; }
        public string details { get; set; }
    }
}
