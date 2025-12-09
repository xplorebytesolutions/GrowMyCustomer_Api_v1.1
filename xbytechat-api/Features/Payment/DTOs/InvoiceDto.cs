#nullable enable
using System;
using System.Collections.Generic;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;

        public Guid BusinessId { get; set; }
        public Guid? SubscriptionId { get; set; }

        public InvoiceStatus Status { get; set; }

        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = "INR";
        public string? AppliedCouponCode { get; set; }

        public DateTime IssuedAtUtc { get; set; }
        public DateTime? DueAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }

        public List<InvoiceLineItemDto> LineItems { get; set; } = new();
    }
}
