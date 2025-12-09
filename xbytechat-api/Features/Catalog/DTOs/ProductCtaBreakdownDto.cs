namespace xbytechat.api.Features.Catalog.DTOs
{
    public class ProductCtaBreakdownDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string CTAJourney { get; set; }
        public int ClickCount { get; set; }
    }
}