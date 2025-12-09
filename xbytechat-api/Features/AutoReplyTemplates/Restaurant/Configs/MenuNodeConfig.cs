namespace xbytechat.api.Features.AutoReplyTemplates.Restaurant.Configs
{
    public class MenuNodeConfig
    {
        public string MenuTitle { get; set; } = string.Empty;         // e.g., "Today's Specials"
        public string Description { get; set; } = string.Empty;       // e.g., "Lunch combos starting at ₹199"
        public string MenuImageUrl { get; set; } = string.Empty;      // CDN or public link
        public string MenuDownloadUrl { get; set; } = string.Empty;   // PDF link or product catalog URL
    }
}
