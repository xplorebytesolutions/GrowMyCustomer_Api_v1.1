using System;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.WhatsAppSettings.Models;

namespace xbytechat_api.WhatsAppSettings.Models
{
    public class WhatsAppSettingEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        // NEW: which provider this row belongs to ("pinnacle", "meta_cloud", "twilio", etc.)
        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } //= "pinnacle";

        [Required]
        [MaxLength(500)]
        public string ApiUrl { get; set; }  // e.g. https://partnersv1.pinbot.ai/v3

        [MaxLength(1000)]
        public string ApiKey { get; set; }

        //[Required]
        //[MaxLength(1000)]
        //public string ApiToken { get; set; } // store encrypted

        //[MaxLength(20)]
       // public string? WhatsAppBusinessNumber { get; set; }

        //public string? PhoneNumberId { get; set; } // used by Meta Cloud; Pinbot doesn't need it
        public string? WabaId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SenderDisplayName { get; set; }

        // Optional: for webhook signature/verification if provider supports it
        [MaxLength(200)]
        public string? WebhookSecret { get; set; }

        [MaxLength(200)]


        public string? WebhookVerifyToken { get; set; }

        [MaxLength(1000)]
        public string? WebhookCallbackUrl { get; set; }


        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<WhatsAppPhoneNumber> WhatsAppBusinessNumbers { get; set; } = new List<WhatsAppPhoneNumber>();

    }
}


