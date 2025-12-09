namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public class CtaButtonDto
    {
        public string Title { get; set; } = string.Empty; // e.g., "Buy Now", "View Details"
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
