using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Features.Entitlements.Models;
using xbytechat.api.Features.Entitlements.Services;
using xbytechat.api.Features.AccessControl.Models; // your Plan/PlanPermission namespace as applicable

namespace xbytechat.api.Features.Entitlements.Services
{
    public sealed class QuotaService : IQuotaService
    {
        private readonly AppDbContext _db;

        public QuotaService(AppDbContext db)
        {
            _db = db;
        }

        // Normalize keys to uppercase to keep lookups stable on non-CI collations
        private static string NK(string key) => key.Trim().ToUpperInvariant();

        private static DateTime CurrentWindowStartUtc(QuotaPeriod p, DateTime nowUtc)
        {
            return p switch
            {
                QuotaPeriod.Daily => new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc),
                QuotaPeriod.Monthly => new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => DateTime.UnixEpoch
            };
        }

        private async Task<(QuotaPeriod Period, long? Limit, string? Denial)> ResolveEffectiveLimitAsync(Guid businessId, string quotaKey, CancellationToken ct)
        {
            quotaKey = NK(quotaKey);

            // resolve business planId
            var business = await _db.Businesses
                .AsNoTracking()
                .Where(b => b.Id == businessId)
                .Select(b => new { b.Id, b.PlanId })
                .FirstOrDefaultAsync(ct);

            if (business is null)
                return (QuotaPeriod.Lifetime, 0, "Business not found.");

            // override first
            var ovr = await _db.BusinessQuotaOverrides
                .AsNoTracking()
                .Where(o => o.BusinessId == businessId && o.QuotaKey.ToUpper() == quotaKey)
                .FirstOrDefaultAsync(ct);

            if (ovr is not null && (ovr.ExpiresAt == null || ovr.ExpiresAt > DateTime.UtcNow))
            {
                if (ovr.IsUnlimited == true)
                    return (QuotaPeriod.Lifetime, null, null); // unlimited

                if (ovr.Limit.HasValue)
                {
                    // Need period: fall back to plan period (must exist)
                    var pq = await _db.PlanQuotas.AsNoTracking()
                        .Where(p => p.PlanId == business.PlanId && p.QuotaKey.ToUpper() == quotaKey)
                        .Select(p => new { p.Period, p.DenialMessage })
                        .FirstOrDefaultAsync(ct);

                    if (pq is null)
                        return (QuotaPeriod.Lifetime, ovr.Limit!.Value, null); // custom limit without period -> treat as lifetime

                    return (pq.Period, ovr.Limit!.Value, pq.DenialMessage);
                }
                // if override exists but no limit/isUnlimited set, fall back to plan
            }

            // plan default
            var planQuota = await _db.PlanQuotas.AsNoTracking()
                .Where(p => p.PlanId == business.PlanId && p.QuotaKey.ToUpper() == quotaKey)
                .FirstOrDefaultAsync(ct);

            if (planQuota is null)
                return (QuotaPeriod.Lifetime, 0, "Quota not defined for plan."); // deny by default

            if (planQuota.Limit < 0)
                return (planQuota.Period, null, planQuota.DenialMessage); // unlimited

            return (planQuota.Period, planQuota.Limit, planQuota.DenialMessage);
        }

        private async Task<BusinessUsageCounter> GetOrCreateCounterAsync(Guid businessId, string quotaKey, QuotaPeriod period, CancellationToken ct)
        {
            quotaKey = NK(quotaKey);
            var now = DateTime.UtcNow;
            var winStart = CurrentWindowStartUtc(period, now);

            var counter = await _db.BusinessUsageCounters.FirstOrDefaultAsync(
                c => c.BusinessId == businessId && c.QuotaKey.ToUpper() == quotaKey &&
                     c.Period == period && c.WindowStartUtc == winStart, ct);

            if (counter is not null) return counter;

            counter = new BusinessUsageCounter
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                QuotaKey = quotaKey,
                Period = period,
                WindowStartUtc = winStart,
                Consumed = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.BusinessUsageCounters.Add(counter);

            try
            {
                await _db.SaveChangesAsync(ct);
                return counter;
            }
            catch (DbUpdateException)
            {
                // Another thread created it; fetch the existing row
                return await _db.BusinessUsageCounters.FirstAsync(
                    c => c.BusinessId == businessId && c.QuotaKey.ToUpper() == quotaKey &&
                         c.Period == period && c.WindowStartUtc == winStart, ct);
            }
        }

        public async Task EnsureWindowAsync(Guid businessId, string quotaKey, CancellationToken ct)
        {
            var (period, _, _) = await ResolveEffectiveLimitAsync(businessId, quotaKey, ct);
            await GetOrCreateCounterAsync(businessId, quotaKey, period, ct);
        }

        public async Task<EntitlementResultDto> CheckAsync(Guid businessId, string quotaKey, long amount, CancellationToken ct)
        {
            quotaKey = NK(quotaKey);
            var (period, limit, denial) = await ResolveEffectiveLimitAsync(businessId, quotaKey, ct);

            if (limit is null) // unlimited
            {
                return new EntitlementResultDto
                {
                    Allowed = true,
                    QuotaKey = quotaKey,
                    Limit = null,
                    Remaining = null
                };
            }

            var counter = await GetOrCreateCounterAsync(businessId, quotaKey, period, ct);

            var remaining = limit.Value - counter.Consumed;
            var allowed = remaining >= amount;

            return new EntitlementResultDto
            {
                Allowed = allowed,
                QuotaKey = quotaKey,
                Limit = limit.Value,
                Remaining = Math.Max(0, remaining),
                Message = allowed ? null : (denial ?? "Quota limit reached.")
            };
        }

        public async Task<EntitlementResultDto> CheckAndConsumeAsync(Guid businessId, string quotaKey, long amount, CancellationToken ct)
        {
            quotaKey = NK(quotaKey);
            var (period, limit, denial) = await ResolveEffectiveLimitAsync(businessId, quotaKey, ct);

            if (limit is null) // unlimited
            {
                // No increment needed; still return success
                return new EntitlementResultDto { Allowed = true, QuotaKey = quotaKey, Limit = null, Remaining = null };
            }

            var now = DateTime.UtcNow;
            var winStart = CurrentWindowStartUtc(period, now);

            // Atomic consume in a single SQL statement
            // UPDATE ... SET Consumed = Consumed + @amount WHERE ... AND Consumed + @amount <= @limit
            var updated = await _db.BusinessUsageCounters
                .Where(c =>
                    c.BusinessId == businessId &&
                    c.QuotaKey.ToUpper() == quotaKey &&
                    c.Period == period &&
                    c.WindowStartUtc == winStart &&
                    c.Consumed + amount <= limit.Value)
                .ExecuteUpdateAsync(up =>
                    up.SetProperty(c => c.Consumed, c => c.Consumed + amount)
                      .SetProperty(c => c.UpdatedAt, _ => now), ct);

            if (updated == 0)
            {
                // Ensure the row exists; if missing, create and retry once
                var existed = await _db.BusinessUsageCounters.AnyAsync(c =>
                    c.BusinessId == businessId &&
                    c.QuotaKey.ToUpper() == quotaKey &&
                    c.Period == period &&
                    c.WindowStartUtc == winStart, ct);

                if (!existed)
                {
                    var counter = new BusinessUsageCounter
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        QuotaKey = quotaKey,
                        Period = period,
                        WindowStartUtc = winStart,
                        Consumed = 0,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.BusinessUsageCounters.Add(counter);
                    await _db.SaveChangesAsync(ct);

                    // retry atomic consume
                    updated = await _db.BusinessUsageCounters
                        .Where(c =>
                            c.BusinessId == businessId &&
                            c.QuotaKey.ToUpper() == quotaKey &&
                            c.Period == period &&
                            c.WindowStartUtc == winStart &&
                            c.Consumed + amount <= limit.Value)
                        .ExecuteUpdateAsync(up =>
                            up.SetProperty(c => c.Consumed, c => c.Consumed + amount)
                              .SetProperty(c => c.UpdatedAt, _ => now), ct);
                }
            }

            if (updated == 0)
            {
                // Denied
                var current = await _db.BusinessUsageCounters.AsNoTracking()
                    .Where(c =>
                        c.BusinessId == businessId &&
                        c.QuotaKey.ToUpper() == quotaKey &&
                        c.Period == period &&
                        c.WindowStartUtc == winStart)
                    .Select(c => c.Consumed)
                    .FirstOrDefaultAsync(ct);

                var remaining = Math.Max(0, limit.Value - current);

                return new EntitlementResultDto
                {
                    Allowed = false,
                    QuotaKey = quotaKey,
                    Limit = limit.Value,
                    Remaining = remaining,
                    Message = denial ?? "Quota limit reached."
                };
            }

            // Success path—fetch updated consumed to compute remaining
            var consumed = await _db.BusinessUsageCounters.AsNoTracking()
                .Where(c =>
                    c.BusinessId == businessId &&
                    c.QuotaKey.ToUpper() == quotaKey &&
                    c.Period == period &&
                    c.WindowStartUtc == winStart)
                .Select(c => c.Consumed)
                .FirstAsync(ct);

            return new EntitlementResultDto
            {
                Allowed = true,
                QuotaKey = quotaKey,
                Limit = limit.Value,
                Remaining = Math.Max(0, limit.Value - consumed)
            };
        }

        public async Task<EntitlementsSnapshotDto> GetSnapshotAsync(Guid businessId, CancellationToken ct)
        {
            // Resolve plan once
            var planId = await _db.Businesses.AsNoTracking()
                .Where(b => b.Id == businessId)
                .Select(b => b.PlanId)
                .FirstAsync(ct);

            var now = DateTime.UtcNow;

            // ✅ Permission codes for this plan (base)
            var planPerms = await _db.PlanPermissions
                .AsNoTracking()
                .Where(pp => pp.PlanId == planId && pp.IsActive && pp.Permission.IsActive)
                .Select(pp => pp.Permission.Code)
                .ToListAsync(ct);

            // ✅ Make it mutable + deduped
            var grantedSet = new HashSet<string>(planPerms, StringComparer.OrdinalIgnoreCase);

            // ✅ Apply BUSINESS permission overrides (grant adds, deny removes)
            // Keeps /entitlements snapshot aligned with JWT minting logic in AuthService
            var permOverrides = await _db.BusinessPermissionOverrides
                .AsNoTracking()
                .Where(o =>
                    o.BusinessId == businessId &&
                    !o.IsRevoked &&
                    (o.ExpiresAtUtc == null || o.ExpiresAtUtc > now) &&
                    o.Permission.IsActive)
                .Select(o => new
                {
                    Code = o.Permission.Code,
                    o.IsGranted
                })
                .ToListAsync(ct);

            foreach (var o in permOverrides)
            {
                if (string.IsNullOrWhiteSpace(o.Code)) continue;

                if (o.IsGranted)
                    grantedSet.Add(o.Code);
                else
                    grantedSet.Remove(o.Code);
            }

            // Quotas – sequential to avoid DbContext concurrency issues
            var planQuotas = await _db.PlanQuotas.AsNoTracking()
                .Where(pq => pq.PlanId == planId)
                .ToListAsync(ct);

            var overrides = await _db.BusinessQuotaOverrides.AsNoTracking()
                .Where(o => o.BusinessId == businessId &&
                            (o.ExpiresAt == null || o.ExpiresAt > now))
                .ToListAsync(ct);

            var items = new List<QuotaSnapshotItemDto>();

            foreach (var pq in planQuotas)
            {
                var key = NK(pq.QuotaKey);

                long? limit = overrides.FirstOrDefault(o => NK(o.QuotaKey) == key) is { } o
                    ? (o.IsUnlimited == true ? null : o.Limit ?? (pq.Limit < 0 ? (long?)null : pq.Limit))
                    : (pq.Limit < 0 ? (long?)null : pq.Limit);

                var winStart = CurrentWindowStartUtc(pq.Period, now);

                var consumed = await _db.BusinessUsageCounters.AsNoTracking()
                    .Where(c => c.BusinessId == businessId &&
                                c.QuotaKey.ToUpper() == key &&
                                c.Period == pq.Period &&
                                c.WindowStartUtc == winStart)
                    .Select(c => c.Consumed)
                    .FirstOrDefaultAsync(ct);

                items.Add(new QuotaSnapshotItemDto
                {
                    QuotaKey = key,
                    Period = pq.Period.ToString(),
                    Limit = limit,
                    Consumed = consumed,
                    Remaining = limit is null ? null : Math.Max(0, limit.Value - consumed),
                    DenialMessage = pq.DenialMessage,
                    WindowStartUtc = winStart.ToString("u")
                });
            }

            return new EntitlementsSnapshotDto
            {
                GrantedPermissions = grantedSet.OrderBy(x => x).ToList(),
                Quotas = items
            };
        }


    }
}
