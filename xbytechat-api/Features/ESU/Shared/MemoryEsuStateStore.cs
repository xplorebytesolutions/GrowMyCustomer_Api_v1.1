using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace xbytechat.api.Features.ESU.Shared
{
    internal sealed class MemoryEsuStateStore : IEsuStateStore
    {
        private readonly IMemoryCache _cache;
        public MemoryEsuStateStore(IMemoryCache cache) => _cache = cache;

        public Task StoreAsync(string state, Guid businessId, TimeSpan ttl)
        {
            _cache.Set(state, businessId, ttl);
            return Task.CompletedTask;
        }

        public Task<(bool Found, Guid BusinessId)> TryConsumeAsync(string state)
        {
            if (_cache.TryGetValue<Guid>(state, out var businessId))
            {
                _cache.Remove(state); // one-time use
                return Task.FromResult((true, businessId));
            }
            return Task.FromResult((false, Guid.Empty));
        }
    }
}
