using System;
using System.Threading.Tasks;

namespace xbytechat.api.Features.ESU.Shared
{
    public interface IEsuStateStore
    {
        Task StoreAsync(string state, Guid businessId, TimeSpan ttl);
        Task<(bool Found, Guid BusinessId)> TryConsumeAsync(string state);
    }
}
