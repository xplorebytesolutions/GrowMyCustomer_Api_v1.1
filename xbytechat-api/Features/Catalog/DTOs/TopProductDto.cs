namespace xbytechat.api.Features.Catalog.DTOs
{
    public class TopProductDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int ClickCount { get; set; }
    }
}