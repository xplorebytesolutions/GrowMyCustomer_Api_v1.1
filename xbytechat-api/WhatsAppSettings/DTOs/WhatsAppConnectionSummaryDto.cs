using System;

namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public class WhatsAppConnectionSummaryDto
    {
        public Guid BusinessId { get; set; }
        public string? WhatsAppBusinessNumber { get; set; }
        public string? PhoneNumberId { get; set; } // Metadata

        // Health Fields
        public string? VerifiedName { get; set; }
        public string? QualityRating { get; set; }
        public string? Status { get; set; }
        public string? NameStatus { get; set; }
        public string? MessagingLimitTier { get; set; }
        
        public DateTime? LastUpdated { get; set; }
    }
}
