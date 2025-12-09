using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Contracts;
using xbytechat.api.Infrastructure;

namespace xbytechat.api.Features.ESU.Facebook.Services
{
    /// <summary>
    /// Centralized reader for Facebook tokens stored in EsuTokens.
    /// Includes in-memory caching and per-key locking to avoid stampedes.
    /// </summary>
    public sealed class FacebookTokenService : IFacebookTokenService
    {
        private const string Provider = "META_CLOUD";

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FacebookTokenService> _log;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public FacebookTokenService(AppDbContext db, IMemoryCache cache, ILogger<FacebookTokenService> log)
        {
            _db = db;
            _cache = cache;
            _log = log;
        }

        private static string CacheKey(Guid biz) => $"esu:fbtoken:{biz:N}";
        private static SemaphoreSlim GetLock(string key) => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        // --- existing methods (TryGetValidAsync, GetRequiredAsync, InvalidateAsync) stay as-is ---

        public async Task<FacebookStoredToken?> TryGetValidAsync(Guid businessId, CancellationToken ct = default)
        {
            var key = CacheKey(businessId);

            if (_cache.TryGetValue<FacebookStoredToken?>(key, out var cached) &&
                cached is not null && !cached.IsExpired() && !cached.WillExpireSoon())
                return cached;

            var gate = GetLock(key);
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue<FacebookStoredToken?>(key, out cached) &&
                    cached is not null && !cached.IsExpired() && !cached.WillExpireSoon())
                    return cached;

                var row = await _db.EsuTokens
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.BusinessId == businessId && x.Provider == Provider && !x.IsRevoked, ct)
                    .ConfigureAwait(false);

                if (row is null || string.IsNullOrWhiteSpace(row.AccessToken)) return null;

                var token = new FacebookStoredToken { AccessToken = row.AccessToken!, ExpiresAtUtc = row.ExpiresAtUtc };
                if (token.IsExpired() || token.WillExpireSoon()) return null;

                var ttl = token.ExpiresAtUtc.HasValue
                    ? TimeSpan.FromMinutes(Math.Min(5, Math.Max(1, (token.ExpiresAtUtc.Value - DateTime.UtcNow).TotalMinutes - 1)))
                    : TimeSpan.FromMinutes(5);

                _cache.Set(key, token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
                return token;
            }
            finally { gate.Release(); }
        }

        public async Task<FacebookStoredToken> GetRequiredAsync(Guid businessId, CancellationToken ct = default)
        {
            var token = await TryGetValidAsync(businessId, ct).ConfigureAwait(false);
            if (token is null) throw new InvalidOperationException("Facebook token missing or expired. Please reconnect ESU.");
            return token;
        }

        // ✅ NEW: implement the interface member that the compiler is asking for
        public async Task<string?> GetAccessTokenAsync(Guid businessId, CancellationToken ct = default)
        {
            var token = await TryGetValidAsync(businessId, ct).ConfigureAwait(false);
            return token?.AccessToken;
        }

        // keep invalidate async so callers can await it consistently
        public Task InvalidateAsync(Guid businessId, CancellationToken ct = default)
        {
            _cache.Remove(CacheKey(businessId));
            return Task.CompletedTask;
        }
    }

}
