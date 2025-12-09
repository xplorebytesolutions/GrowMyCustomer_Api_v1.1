using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class CampaignRecipient
    {
        public Guid Id { get; set; }

        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }   // nav is optional at runtime

        public Guid? ContactId { get; set; }      // ← optional FK
        public Contact? Contact { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Sent, Delivered, Failed, Replied
        public DateTime? SentAt { get; set; }

        public string? BotId { get; set; }
        public string? MessagePreview { get; set; }
        public string? ClickedCTA { get; set; }
        public string? CategoryBrowsed { get; set; }
        public string? ProductBrowsed { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsAutoTagged { get; set; } = false;

        // Logs
        public ICollection<CampaignSendLog> SendLogs { get; set; } = new List<CampaignSendLog>();

        public Guid BusinessId { get; set; }
        public Business? Business { get; set; }

        public Guid? AudienceMemberId { get; set; }
        public AudienceMember? AudienceMember { get; set; } = null!;

        [Column(TypeName = "jsonb")]
        public string? ResolvedParametersJson { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ResolvedButtonUrlsJson { get; set; }

        public string? IdempotencyKey { get; set; }
        public DateTime? MaterializedAt { get; set; }
    }
}


//using System;
//using System.Collections.Generic;
//using xbytechat.api.CRM.Models;
//using xbytechat.api.Features.BusinessModule.Models;
//using xbytechat.api.Features.CampaignTracking.Models;

//namespace xbytechat.api.Features.CampaignModule.Models
//{
//    public class CampaignRecipient
//    {
//        public Guid Id { get; set; }

//        public Guid CampaignId { get; set; }
//        public Campaign Campaign { get; set; }

//        public Guid? ContactId { get; set; }
//        public Contact Contact { get; set; }

//        public string Status { get; set; } = "Pending"; // Pending, Sent, Delivered, Failed, Replied
//        public DateTime? SentAt { get; set; }

//        public string? BotId { get; set; } // Multi-bot support
//        public string? MessagePreview { get; set; } // Final message sent
//        public string? ClickedCTA { get; set; } // Track CTA clicked like "BuyNow"
//        public string? CategoryBrowsed { get; set; } // e.g., Ads
//        public string? ProductBrowsed { get; set; } // e.g., Product name
//        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

//        public bool IsAutoTagged { get; set; } = false; // Flag for automation-based tagging

//        // ✅ NEW: One-to-many link to detailed logs (message attempts, delivery tracking)
//        public ICollection<CampaignSendLog> SendLogs { get; set; }

//        public Guid BusinessId { get; set; }  // ✅ Add this line
//        public Business Business { get; set; } = null!; // if navigation is needed

//        // If this recipient originated from an Audience upload (CSV), link it here
//        public Guid? AudienceMemberId { get; set; }
//        // Resolved template parameters for this recipient (body/header placeholders)
//        // Example: ["Nicola","500OFF"]
//        public string? ResolvedParametersJson { get; set; }

//        // Resolved final URLs for buttons (index-aligned: 0,1,2)
//        // Example: ["https://lnk.xbyte/r/abc", "https://lnk.xbyte/r/def"]
//        public string? ResolvedButtonUrlsJson { get; set; }

//        // An idempotency fingerprint for the specific send to this recipient
//        // (e.g., SHA256(CampaignId|PhoneE164|TemplateName|ResolvedParametersJson|ResolvedButtonUrlsJson))
//        public string? IdempotencyKey { get; set; }

//        // When this recipient was materialized (frozen) and ready to dispatch
//        public DateTime? MaterializedAt { get; set; }

//    }
//}
