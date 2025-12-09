namespace xbytechat.api.Features.ESU.Shared
{
    public sealed class EsuOptions
    {
        public FacebookEsuOptions Facebook { get; set; } = new();
    }

    public sealed class FacebookEsuOptions
    {
        public string AppId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string Scopes { get; set; } = "whatsapp_business_management,whatsapp_business_messaging";
        public int StateTtlMinutes { get; set; } = 20;

        public string? ConfigId { get; set; }
    }
}
