#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Payment.DTOs;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Handles business-facing subscription lifecycle:
    /// trial, activation, upgrade/downgrade, cancellation.
    /// </summary>
    public interface ISubscriptionService
    {
        Task<SubscriptionDto?> GetCurrentForBusinessAsync(Guid businessId, CancellationToken ct = default);

        Task<SubscriptionDto> CreateOrUpdateSubscriptionAsync(
            Guid businessId,
            CreateSubscriptionRequestDto request,
            CancellationToken ct = default);

        Task<bool> MarkCancelAtPeriodEndAsync(Guid businessId, CancellationToken ct = default);

        Task<bool> ReactivateAutoRenewAsync(Guid businessId, CancellationToken ct = default);
    }
}

