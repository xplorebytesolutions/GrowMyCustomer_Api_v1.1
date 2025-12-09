#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.BusinessModule.Models;

namespace xbytechat.api.Features.Payment.Models
{
    /// <summary>
    /// Represents a single payment interaction with a gateway
    /// (checkout session, order, capture, refund, etc.).
    /// </summary>
    public class PaymentTransaction
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Business that owns this transaction.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Optional related subscription (plan change, renewal, etc.).
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// Optional related invoice (if this payment settles a specific invoice).
        /// </summary>
        public Guid? InvoiceId { get; set; }

        /// <summary>
        /// Total amount in smallest currency unit or decimal depending on your standard.
        /// Use decimal for INR and similar.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// ISO currency code, e.g. "INR", "USD".
        /// </summary>
        public string Currency { get; set; } = "INR";

        /// <summary>
        /// Current status of the transaction (pending, success, failed, etc.).
        /// </summary>
        public PaymentStatus Status { get; set; }

        /// <summary>
        /// Gateway name/provider identifier (e.g. "Razorpay", "Stripe").
        /// Kept as string for flexibility; can be enum'ed later.
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// Payment/charge id from the gateway.
        /// </summary>
        public string? GatewayPaymentId { get; set; }

        /// <summary>
        /// Order/checkout/session id from the gateway (if applicable).
        /// </summary>
        public string? GatewayOrderId { get; set; }

        /// <summary>
        /// Optional gateway signature or verification token (for validation).
        /// </summary>
        public string? GatewaySignature { get; set; }

        /// <summary>
        /// In case of failure, store short reason for debugging / UI.
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Any extra metadata (JSON) for debugging or support audits.
        /// </summary>
        public string? MetaJson { get; set; }

        /// <summary>
        /// When this record was created (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// When transaction was completed (success/failure/refund), if known (UTC).
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        // ---- Navigation ----
        public Business? Business { get; set; }
        public Subscription? Subscription { get; set; }
        public Invoice? Invoice { get; set; }
    }
}
