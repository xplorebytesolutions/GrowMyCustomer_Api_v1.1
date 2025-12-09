#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Minimal coupon validator:
    /// - checks code, active flag, date range.
    /// - does NOT yet enforce redemption limits.
    /// </summary>
    public sealed class CouponService : ICouponService
    {
        private readonly AppDbContext _db;

        public CouponService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CouponDto?> ValidateCouponAsync(
            string code,
            string currency,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var now = DateTime.UtcNow;
            var coupon = await _db.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                        c.IsActive &&
                        c.Code == code &&
                        (c.ValidFromUtc == null || c.ValidFromUtc <= now) &&
                        (c.ValidToUtc == null || c.ValidToUtc >= now),
                    ct);

            if (coupon is null)
                return null;

            // Future:
            // - Validate currency, plan scope, usage limits.
            // - For now we trust currency compatibility implicitly.

            return new CouponDto
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Description = coupon.Description,
                DiscountType = coupon.DiscountType,
                DiscountValue = coupon.DiscountValue,
                IsActive = coupon.IsActive,
                ValidFromUtc = coupon.ValidFromUtc,
                ValidToUtc = coupon.ValidToUtc
            };
        }
    }
}
