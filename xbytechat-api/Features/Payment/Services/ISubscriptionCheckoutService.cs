#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Payment.DTOs;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Orchestrates plan selection -> invoice -> payment session for subscriptions.
    /// </summary>
    public interface ISubscriptionCheckoutService
    {
        Task<PaymentSessionResponseDto> StartSubscriptionCheckoutAsync(
            Guid businessId,
            CreateSubscriptionRequestDto request,
            CancellationToken ct = default);
    }
}
