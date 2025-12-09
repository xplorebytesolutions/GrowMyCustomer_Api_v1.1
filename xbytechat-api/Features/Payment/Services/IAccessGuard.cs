#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Central guard to decide if a business is allowed to use billable features.
    /// </summary>
    public interface IAccessGuard
    {
        Task<bool> CanUseCoreFeaturesAsync(Guid businessId, CancellationToken ct = default);
        Task<AccessCheckResult> CheckAsync(Guid businessId, CancellationToken ct = default);
    }
}
