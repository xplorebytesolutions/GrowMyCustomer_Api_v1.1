using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.ESU.Shared
{
    public interface IEsuFlagStore
    {
     

        Task<bool> IsFacebookEsuCompletedAsync(
            Guid businessId,
            CancellationToken ct = default);

        Task UpsertAsync(Guid businessId, string key, string value, string? jsonPayload = null, CancellationToken ct = default);
        Task DeleteAsync(Guid businessId, CancellationToken ct = default);
    }
}
