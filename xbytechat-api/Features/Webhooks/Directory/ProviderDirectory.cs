using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.Webhooks.Directory
{
    /// <summary>
    /// EF-backed resolver for mapping provider identifiers to BusinessId, with a short cache.
    /// </summary>
    public class ProviderDirectory : IProviderDirectory
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProviderDirectory> _logger;
        private readonly IMemoryCache _cache;

        // reduce DB hits during webhook bursts
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        public ProviderDirectory(AppDbContext db, ILogger<ProviderDirectory> logger, IMemoryCache cache)
        {
            _db = db;
            _logger = logger;
            _cache = cache;
        }
        private static string NormalizeProviderKey(string? raw)
        {
            var p = (raw ?? "").Trim().ToLowerInvariant();
            return p switch
            {
                "meta" or "meta-cloud" or "meta_cloud" => "meta_cloud",
                "pinnacle" => "pinnacle",
                _ => p
            };
        }
        public async Task<Guid?> ResolveBusinessIdAsync(
            string? provider,
            string? phoneNumberId,
            string? displayPhoneNumber,
            string? wabaId,
            string? waId,
            CancellationToken ct = default)
        {
            provider = NormalizeProviderKey(provider);
            if (provider is "meta" or "meta-cloud") provider = "meta_cloud";
            var cacheKey = $"provdir:{provider}:{phoneNumberId}:{Normalize(displayPhoneNumber)}:{wabaId}";
            if (_cache.TryGetValue<Guid?>(cacheKey, out var cached))
                return cached;

            try
            {
                // ⚓ 1) Strongest match: provider + phone_number_id
                if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    var hit = await QueryByAsync(provider, phoneNumberId: phoneNumberId, ct: ct);
                    if (hit.HasValue) return CacheAndReturn(cacheKey, hit);
                }

                // ⚓ 2) Next: provider + display_phone_number (normalized)
                var normalizedDisplay = Normalize(displayPhoneNumber);
                if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(normalizedDisplay))
                {
                    var hit = await QueryByAsync(provider, displayPhoneNumber: normalizedDisplay, ct: ct);
                    if (hit.HasValue) return CacheAndReturn(cacheKey, hit);
                }

                // ⚓ 3) Next: provider + wabaId (Meta)
                if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(wabaId))
                {
                    var hit = await QueryByAsync(provider, wabaId: wabaId, ct: ct);
                    if (hit.HasValue) return CacheAndReturn(cacheKey, hit);
                }

                _logger.LogWarning(
                    "ProviderDirectory: No match for provider={Provider}, pnid={PhoneId}, disp={Display}, waba={Waba}",
                    provider, phoneNumberId, normalizedDisplay, wabaId
                );
                return CacheAndReturn(cacheKey, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProviderDirectory lookup failed.");
                return null;
            }
        }

        private Guid? CacheAndReturn(string key, Guid? value)
        {
            _cache.Set(key, value, CacheTtl);
            return value;
        }

        /// <summary>
        /// Centralized query; now materializes candidates so Normalize() runs in-memory.
        /// </summary>
        private async Task<Guid?> QueryByAsync(
         string provider,
         string? phoneNumberId = null,
         string? displayPhoneNumber = null,
         string? wabaId = null,
         CancellationToken ct = default)
        {
            // Canonical provider (we always store in DB as CAPS)
            provider = (provider ?? string.Empty)
                .Trim()
                .Replace("-", "_")
                .Replace(" ", "_")
                .ToUpperInvariant();

            if (provider is not ("PINNACLE" or "META_CLOUD"))
                return null;

            var normDisp = Normalize(displayPhoneNumber); // your existing normalization (digits-only etc.)

            // 1) Prefer exact match by sender (PhoneNumberId or display number) from WhatsAppPhoneNumbers
            if (!string.IsNullOrWhiteSpace(phoneNumberId) || !string.IsNullOrWhiteSpace(normDisp))
            {
                var byNumber = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(n => n.Provider == provider)
                    .Where(n =>
                        (!string.IsNullOrWhiteSpace(phoneNumberId) && n.PhoneNumberId == phoneNumberId) ||
                        (!string.IsNullOrWhiteSpace(normDisp) && Normalize(n.WhatsAppBusinessNumber) == normDisp))
                    .OrderByDescending(n => n.IsDefault)
                    .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                    .Select(n => (Guid?)n.BusinessId)
                    .FirstOrDefaultAsync(ct);

                if (byNumber.HasValue)
                    return byNumber;
            }

            // 2) Fallback: match by WABA on the settings row (still lives on settings)
            if (!string.IsNullOrWhiteSpace(wabaId))
            {
                var byWaba = await _db.WhatsAppSettings
                    .AsNoTracking()
                    .Where(s => s.IsActive
                                && s.Provider.ToUpper() == provider
                                && s.WabaId == wabaId)
                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                    .Select(s => (Guid?)s.BusinessId)
                    .FirstOrDefaultAsync(ct);

                if (byWaba.HasValue)
                    return byWaba;
            }

            return null;
        }

        /// <summary>
        /// Normalize phone formatting for robust comparisons.
        /// </summary>
        private static string? Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var trimmed = phone.Trim();
            var hasPlus = trimmed.StartsWith("+");
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            return hasPlus ? "+" + digits : digits;
        }
    }
}



