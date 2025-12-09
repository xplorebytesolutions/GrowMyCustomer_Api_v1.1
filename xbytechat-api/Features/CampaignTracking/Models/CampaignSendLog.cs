using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CampaignTracking.Models
{
    public class CampaignSendLog
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? RunId { get; set; }
        public string? MessageId { get; set; } // Unique WAMID from WhatsApp
        // 🔗 Foreign Keys
    
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

 
        public Guid? ContactId { get; set; }

        [Required]
        public Guid RecipientId { get; set; }

        // 📩 Message Info
        [Required]
        public string MessageBody { get; set; } = "";

        public string? TemplateId { get; set; }
        public string? SendStatus { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        // 🌐 Metadata
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
        public string? MacAddress { get; set; }
        public string? SourceChannel { get; set; }

        // ✅ UX-Derived
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }

        // 📊 Click Tracking
        public bool IsClicked { get; set; } = false;
        public DateTime? ClickedAt { get; set; }
        public string? ClickType { get; set; }

        // 🔁 Retry Tracking (💡 New)
        public int RetryCount { get; set; } = 0;                 // Number of retry attempts
        public DateTime? LastRetryAt { get; set; }               // When retry last happened
        public string? LastRetryStatus { get; set; }             // Success / Failed
        public bool AllowRetry { get; set; } = true;             // Flag to enable/disable retry

        // 👁 Navigation
      
        public Contact? Contact { get; set; }
        public CampaignRecipient? Recipient { get; set; }


        // 🔗 MessageLog reference (optional)
        public Guid? MessageLogId { get; set; }
        public MessageLog? MessageLog { get; set; }

        public Guid BusinessId { get; set; }

        // 🆕 Flow context snapshot for deterministic click resolution
        public Guid? CTAFlowConfigId { get; set; }   // which flow this send belongs to (optional)
        public Guid? CTAFlowStepId { get; set; }     // the entry step id (optional)

        [Column(TypeName = "text")]
        public string? ButtonBundleJson { get; set; }


    }
}
