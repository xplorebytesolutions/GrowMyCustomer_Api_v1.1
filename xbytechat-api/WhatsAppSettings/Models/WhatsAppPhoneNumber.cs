using System;

namespace xbytechat.api.Features.WhatsAppSettings.Models
{
    public class WhatsAppPhoneNumber
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BusinessId { get; set; }
        public string Provider { get; set; } = null!;                 // "Meta_cloud" | "Pinnacle" | etc.

        public string PhoneNumberId { get; set; } = null!;            // provider-specific id (e.g., Meta phone_number_id)
        public string WhatsAppBusinessNumber { get; set; } = null!;   // e.g., "+15551234567"
        public string? SenderDisplayName { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
