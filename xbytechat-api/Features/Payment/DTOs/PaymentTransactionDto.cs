#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    public class PaymentTransactionDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid? SubscriptionId { get; set; }
        public Guid? InvoiceId { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";

        public PaymentStatus Status { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string? GatewayPaymentId { get; set; }
        public string? GatewayOrderId { get; set; }

        public string? FailureReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}

