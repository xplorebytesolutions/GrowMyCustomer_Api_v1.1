namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public sealed class WhatsAppSettingsDto
    {
        public Guid BusinessId { get; init; }
        public string Provider { get; init; } = string.Empty;
        public string ApiUrl { get; init; } = string.Empty;
        public string? ApiKey { get; init; }
        public string? WabaId { get; init; }

        public string? PhoneNumberId { get; init; }  // from WhatsAppPhoneNumbers
        public string? WhatsAppBusinessNumber { get; init; }  // from WhatsAppPhoneNumbers
    }

}
