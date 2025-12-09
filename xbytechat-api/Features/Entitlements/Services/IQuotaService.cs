using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Entitlements.DTOs;

namespace xbytechat.api.Features.Entitlements.Services
{
    public interface IQuotaService
    {
        Task<EntitlementResultDto> CheckAsync(Guid businessId, string quotaKey, long amount, CancellationToken ct);
        Task<EntitlementResultDto> CheckAndConsumeAsync(Guid businessId, string quotaKey, long amount, CancellationToken ct);

        Task<EntitlementsSnapshotDto> GetSnapshotAsync(Guid businessId, CancellationToken ct);

        // Utility to ensure counters are on the correct window (creates or rolls window if needed)
        Task EnsureWindowAsync(Guid businessId, string quotaKey, CancellationToken ct);
    }
}
