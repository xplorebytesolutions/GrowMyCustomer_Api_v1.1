namespace xbytechat.api.Features.CampaignModule.CountryCodes
{
    public sealed class WhatsAppPhoneValidationOptions
    {
        public string Mode { get; set; } = "allow"; // "allow" or "deny"
        public List<string> AllowedCountryCodes { get; set; } = new();
    }
}
