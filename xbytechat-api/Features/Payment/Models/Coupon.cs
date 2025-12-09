#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.Models
{
    /// <summary>
    /// Represents a promotional coupon that can apply discounts
    /// on subscriptions or invoices (like big players do).
    /// </summary>
    public class Coupon
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Unique code entered by the user, e.g. "LAUNCH20".
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-friendly description for admins.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// How the discount is calculated (fixed / percentage).
        /// </summary>
        public DiscountType DiscountType { get; set; }

        /// <summary>
        /// Discount value:
        /// - If Percentage: 10 = 10%
        /// - If FixedAmount: absolute amount in invoice currency (e.g. 500 = ₹500).
        /// </summary>
        public decimal DiscountValue { get; set; }

        /// <summary>
        /// Optional: limit total number of times the coupon can be used globally.
        /// Null = unlimited.
        /// </summary>
        public int? MaxRedemptions { get; set; }

        /// <summary>
        /// Optional: limit per business usage count.
        /// Null = no per-business cap.
        /// </summary>
        public int? MaxRedemptionsPerBusiness { get; set; }

        /// <summary>
        /// Optional: apply only to a specific plan.
        /// Null = can apply to any eligible plan.
        /// </summary>
        public Guid? PlanId { get; set; }

        /// <summary>
        /// Start time of coupon validity (UTC).
        /// </summary>
        public DateTime? ValidFromUtc { get; set; }

        /// <summary>
        /// End time of coupon validity (UTC).
        /// </summary>
        public DateTime? ValidToUtc { get; set; }

        /// <summary>
        /// Whether this coupon is currently active (admin toggle).
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Optional metadata or rules (JSON) for future logic.
        /// </summary>
        public string? MetaJson { get; set; }
    }
}
