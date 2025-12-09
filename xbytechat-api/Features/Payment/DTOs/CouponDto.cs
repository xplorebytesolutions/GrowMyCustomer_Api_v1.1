#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    public class CouponDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }

        public bool IsActive { get; set; }
        public DateTime? ValidFromUtc { get; set; }
        public DateTime? ValidToUtc { get; set; }
    }
}

