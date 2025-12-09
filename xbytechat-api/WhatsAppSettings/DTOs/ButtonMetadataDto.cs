namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public class ButtonMetadataDto
    {
        public string Type { get; set; } // Example: "URL" or "PHONE_NUMBER"
        public string Text { get; set; } // Button Text
        public string SubType { get; set; } // (optional) for URL, Phone Number etc
        public int Index { get; set; } // Index like 0, 1
                                       // Optional: dynamic parameter value for validation
        public string? ParameterValue { get; set; } // e.g. coupon_code
    }

}
