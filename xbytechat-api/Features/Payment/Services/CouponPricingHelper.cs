#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Small helper to apply coupon math consistently.
    /// This does not check validity windows or max usage - that stays in CouponService.
    /// </summary>
    internal static class CouponPricingHelper
    {
        public static (decimal discountAmount, decimal finalAmount) ApplyCoupon(
            Coupon coupon,
            decimal subtotal)
        {
            if (subtotal <= 0 || !coupon.IsActive)
                return (0m, subtotal);

            decimal discount = coupon.DiscountType switch
            {
                DiscountType.Percentage =>
                    Math.Round(subtotal * (coupon.DiscountValue / 100m), 2, MidpointRounding.AwayFromZero),

                DiscountType.FixedAmount =>
                    Math.Min(coupon.DiscountValue, subtotal),

                _ => 0m
            };

            var final = subtotal - discount;
            if (final < 0) final = 0;

            return (discount, final);
        }
    }
}

