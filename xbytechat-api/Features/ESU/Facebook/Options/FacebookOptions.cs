namespace xbytechat.api.Features.ESU.Facebook.Options
{
    public sealed class FacebookOptions
    {
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;

        // add these if not present
        public string? Scopes { get; set; }
        public string? GraphBaseUrl { get; set; }
        public string? GraphApiVersion { get; set; }
        public int StateTtlMinutes { get; set; } = 20;

        // NEW:
        public string? VerifyToken { get; set; }
    }
}
