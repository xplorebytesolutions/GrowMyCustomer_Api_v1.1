#nullable enable
using System;

namespace xbytechat.api.Features.Payment.DTOs
{
    /// <summary>
    /// Request to start a payment for a subscription or invoice.
    /// Maps to a gateway checkout / payment link.
    /// </summary>
    public class CreatePaymentSessionRequestDto
    {
        /// <summary>
        /// Optional: target subscription change (upgrade/downgrade/renewal).
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// Optional: invoice to pay. If null, backend can construct
        /// a new invoice based on plan selection.
        /// </summary>
        public Guid? InvoiceId { get; set; }

        /// <summary>
        /// Optional coupon code to apply at time of payment.
        /// </summary>
        public string? CouponCode { get; set; }
    }
}
