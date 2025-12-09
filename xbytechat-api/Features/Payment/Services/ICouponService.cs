#nullable enable
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Payment.DTOs;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Validates coupons and exposes safe coupon info.
    /// Admin management can be a separate module.
    /// </summary>
    public interface ICouponService
    {
        Task<CouponDto?> ValidateCouponAsync(
            string code,
            string currency,
            CancellationToken ct = default);
    }
}

