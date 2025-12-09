#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    /// <summary>
    /// Request to start or change a subscription for the current business.
    /// Payment authorization will be handled via a separate payment flow.
    /// </summary>
    public class CreateSubscriptionRequestDto
    {
        /// <summary>
        /// Target plan to subscribe to.
        /// </summary>
        public Guid PlanId { get; set; }

        /// <summary>
        /// Selected billing cycle (Monthly / Yearly).
        /// </summary>
        public BillingCycle BillingCycle { get; set; }

        /// <summary>
        /// Optional coupon code the user wants to apply.
        /// </summary>
        public string? CouponCode { get; set; }
    }
}
