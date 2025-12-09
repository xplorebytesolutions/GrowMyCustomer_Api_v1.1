#nullable enable

namespace xbytechat.api.Features.ESU.Facebook.Options
{
    public sealed class FacebookOauthOptions
    {
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string GraphBaseUrl { get; set; } = "https://graph.facebook.com";
        public string GraphApiVersion { get; set; } = "v22.0";
    }
}
