#nullable enable
using System;

namespace xbytechat.api.Features.Payment.DTOs
{
    public class InvoiceLineItemDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
