namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public class WhatsAppSenderDto
    {
        public Guid Id { get; set; }                         // row id in WhatsAppPhoneNumbers
        public Guid BusinessId { get; set; }
        public string Provider { get; set; } = string.Empty; // "PINNACLE" | "META_CLOUD"
        public string PhoneNumberId { get; set; } = string.Empty;
        public string WhatsAppBusinessNumber { get; set; } = string.Empty; // E.164 printable
        public string? SenderDisplayName { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(SenderDisplayName)
                ? $"{WhatsAppBusinessNumber} • {Provider}"
                : $"{SenderDisplayName} ({WhatsAppBusinessNumber}) • {Provider}";
    }
}
