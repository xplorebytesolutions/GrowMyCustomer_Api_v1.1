namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignButtonDto
    {
        public string ButtonText { get; set; } = string.Empty; // 📍 e.g., "Buy Now"
        public string ButtonType { get; set; } = "url";         // 🔘 url | quick_reply | call
        public string TargetUrl { get; set; } = string.Empty;  // 🌐 or phone/call param
    }
}
