#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Models; // IntegrationFlags (plural)
using xbytechat.api.Infrastructure;               // AppDbContext

namespace xbytechat.api.Features.ESU.Shared
{

    public sealed class EsuFlagStore : IEsuFlagStore
    {
        private readonly AppDbContext _db;
        private readonly ILogger<EsuFlagStore> _log;
        private readonly IMemoryCache _cache;
        private readonly EsuFlagCacheOptions _cacheOpts;

        public EsuFlagStore(
            AppDbContext db,
            ILogger<EsuFlagStore> log,
            IMemoryCache cache,
            IOptions<EsuFlagCacheOptions> cacheOpts)
        {
            _db = db;
            _log = log;
            _cache = cache;
            _cacheOpts = cacheOpts.Value;
        }

        // ---- cache helpers ---------------------------------------------------
        private static string CacheKey(Guid businessId) => $"esu:intflags:{businessId:N}";
        private void Invalidate(Guid businessId) => _cache.Remove(CacheKey(businessId));

        private async Task<IntegrationFlags?> GetRowAsync(Guid businessId, CancellationToken ct)
        {
            if (_cache.TryGetValue<IntegrationFlags?>(CacheKey(businessId), out var cached))
                return cached;

            var row = await _db.IntegrationFlags
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct)
                .ConfigureAwait(false);

            var ttl = row is null
                ? TimeSpan.FromSeconds(_cacheOpts.MissTtlSeconds)
                : TimeSpan.FromSeconds(_cacheOpts.TtlSeconds);

            _cache.Set(CacheKey(businessId), row, ttl);
            return row;
        }

        // ---- legacy-shaped APIs (kept for back-compat) ----------------------

        public async Task<IntegrationFlags?> GetAsync(Guid businessId, string key, CancellationToken ct = default)
            => await GetRowAsync(businessId, ct).ConfigureAwait(false);

        public async Task<string?> GetValueAsync(Guid businessId, string key, CancellationToken ct = default)
        {
            var row = await GetRowAsync(businessId, ct).ConfigureAwait(false);
            if (row is null) return null;

            if (string.Equals(key, "FACEBOOK_ESU_COMPLETED", StringComparison.OrdinalIgnoreCase))
                return row.FacebookEsuCompleted ? "true" : "false";

            return null; // unknown legacy key in the explicit-column model
        }


        public async Task UpsertAsync(
      Guid businessId,
      string key,
      string value,
      string? jsonPayload = null,   // kept for API compatibility; ignored for flags
      CancellationToken ct = default)
        {
            // Load/create the per-business row
            var row = await _db.IntegrationFlags
                .AsTracking()
                .FirstOrDefaultAsync(x => x.BusinessId == businessId, ct);

            if (row == null)
            {
                row = new IntegrationFlags
                {
                    BusinessId = businessId,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.IntegrationFlags.Add(row);
            }

            // 🚫 No secret handling here. Tokens live in EsuTokens only.

            // Mark ESU completed when we get the canonical key/value
            // (do not flip it back to false if someone passes another value later)
            if (string.Equals(key, "facebook.esu", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value, "completed", StringComparison.OrdinalIgnoreCase))
            {
                row.FacebookEsuCompleted = true;
            }

            // Touch timestamp; interceptors may also update this
            row.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            // Clear any in-process caches (IMemoryCache Remove is sync)
            Invalidate(businessId);
        }

       

        public async Task<bool> IsFacebookEsuCompletedAsync(Guid businessId, CancellationToken ct = default)
        {
            var row = await GetRowAsync(businessId, ct).ConfigureAwait(false);
            return row?.FacebookEsuCompleted == true;
        }

        // FILE: Features/ESU/Facebook/Services/EsuFlagStore.cs

        public async Task DeleteAsync(Guid businessId, CancellationToken ct = default)
        {
            var row = await _db.IntegrationFlags
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct)
                .ConfigureAwait(false);

            if (row is null)
                return;

            _db.IntegrationFlags.Remove(row);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            Invalidate(businessId);
        }

    }
}
