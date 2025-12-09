// 📄 Features/CampaignTracking/Config/TrackingOptions.cs
namespace xbytechat.api.Features.CampaignTracking.Config
{
    public class TrackingOptions
    {
        public string BaseUrl { get; set; } = "";
        public string Secret { get; set; } = "";
        public TimeSpan TokenTtl { get; set; } = TimeSpan.FromDays(30);
    }
}
