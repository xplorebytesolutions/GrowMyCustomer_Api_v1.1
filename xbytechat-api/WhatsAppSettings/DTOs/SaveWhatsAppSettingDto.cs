// 📄 File: WhatsAppSettings/DTOs/SaveWhatsAppSettingDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat_api.WhatsAppSettings.DTOs
{
    public class SaveWhatsAppSettingDto
    {
        public Guid BusinessId { get; set; }

        // Which provider: "pinnacle" | "meta_cloud"
        [Required, MaxLength(50)]
        public string Provider { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string ApiUrl { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? ApiKey { get; set; } // Pinnacle

      
        [MaxLength(100)]
        public string? PhoneNumberId { get; set; } // Meta Cloud

        [MaxLength(100)]
        public string? WabaId { get; set; } // Optional (Pinnacle/Meta)

        [MaxLength(50)]
        public string? WhatsAppBusinessNumber { get; set; }

        [MaxLength(100)]
        public string? SenderDisplayName { get; set; }

        [MaxLength(200)]
        public string? WebhookSecret { get; set; }

        [MaxLength(200)]
        public string? WebhookVerifyToken { get; set; }

        // 👇 NEW: per-provider callback URL (optional, stored in DB)
        [MaxLength(1000)]
        public string? WebhookCallbackUrl { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
