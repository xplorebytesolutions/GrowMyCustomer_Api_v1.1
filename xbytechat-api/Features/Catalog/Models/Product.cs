// File: Features/Catalog/Models/Product.cs

using System;

namespace xbytechat.api.Features.Catalog.Models
{
    public class Product
    {
        public Guid Id { get; set; }

        // 🔗 Foreign Key - Business/Owner
        public Guid BusinessId { get; set; }

        // 📦 Core Product Info
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "INR";
        public string ImageUrl { get; set; } = string.Empty;

        // ✅ Visibility & State
        public bool IsActive { get; set; } = true;

        // 📅 Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int TotalClicks { get; set; } = 0; // Total CTA clicks tracked
        public DateTime? LastClickedAt { get; set; } // Last time a user clicked CTA for this product
        public string? MostClickedCTA { get; set; } // Button text with highest click count (e.g., "Buy Now")

    }
}
