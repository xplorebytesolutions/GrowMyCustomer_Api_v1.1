// File: Features/Catalog/DTOs/CreateProductDto.cs

namespace xbytechat.api.Features.Catalog.DTOs
{
    public class CreateProductDto
    {
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "INR";
        public string ImageUrl { get; set; } = string.Empty;
    }
}
