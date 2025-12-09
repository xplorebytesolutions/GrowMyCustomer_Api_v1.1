// 📄 Infrastructure/Flows/FlowClickTokenOptions.cs
namespace xbytechat.api.Infrastructure.Flows
{
    public class FlowClickTokenOptions
    {
        public string Secret { get; set; } = "";   // long random string (256-bit recommended)
        public string BaseUrl { get; set; } = "";  // e.g. https://app.yourdomain.com
        public int TtlHours { get; set; } = 72;    // token lifetime (default 3 days)
    }
}
