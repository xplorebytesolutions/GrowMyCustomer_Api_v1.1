// 📄 xbytechat_api/WhatsAppSettings/Services/WhatsAppSettingsService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api;
using xbytechat.api.Features.WhatsAppSettings.Models;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat_api.WhatsAppSettings.Services
{
    public class WhatsAppSettingsService : IWhatsAppSettingsService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public WhatsAppSettingsService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SaveOrUpdateSettingAsync(SaveWhatsAppSettingDto dto)
        {
            if (dto.BusinessId == Guid.Empty) throw new ArgumentException("BusinessId required");

            // Canonical provider: PINNACLE | META_CLOUD
            var providerCanon = (dto.Provider ?? "PINNACLE")
                .Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            if (providerCanon is not ("PINNACLE" or "META_CLOUD")) providerCanon = "PINNACLE";

            var now = DateTime.UtcNow;

            // 1. Check for duplicate WabaId across other businesses (if provided)
            if (!string.IsNullOrWhiteSpace(dto.WabaId))
            {
                var cleanWaba = dto.WabaId.Trim();
                var conflict = await _dbContext.WhatsAppSettings
                    .AsNoTracking()
                    .AnyAsync(x => x.BusinessId != dto.BusinessId && 
                                   x.Provider == providerCanon && 
                                   x.WabaId == cleanWaba);
                
                if (conflict)
                {
                    throw new InvalidOperationException($"The WABA ID '{cleanWaba}' is already associated with another account. Please verify your credentials.");
                }
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync();

            // Exact business + provider row
            var existing = await _dbContext.WhatsAppSettings
                .FirstOrDefaultAsync(x => x.BusinessId == dto.BusinessId &&
                                          x.Provider.ToLower() == providerCanon.ToLower());

            // Use existing casing if found to avoid Principal Key update (FK mismatch)
            var effectiveProvider = existing?.Provider ?? providerCanon;

            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.ApiUrl)) existing.ApiUrl = dto.ApiUrl.Trim();
                if (!string.IsNullOrWhiteSpace(dto.ApiKey)) existing.ApiKey = dto.ApiKey.Trim();
                if (!string.IsNullOrWhiteSpace(dto.WabaId)) existing.WabaId = dto.WabaId.Trim();
                if (!string.IsNullOrWhiteSpace(dto.SenderDisplayName)) existing.SenderDisplayName = dto.SenderDisplayName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.WebhookSecret)) existing.WebhookSecret = dto.WebhookSecret.Trim();
                if (!string.IsNullOrWhiteSpace(dto.WebhookVerifyToken)) existing.WebhookVerifyToken = dto.WebhookVerifyToken.Trim();
                if (!string.IsNullOrWhiteSpace(dto.WebhookCallbackUrl)) existing.WebhookCallbackUrl = dto.WebhookCallbackUrl.Trim();

                existing.IsActive = dto.IsActive;
                existing.UpdatedAt = now;

                // Ensure single active row per business
                await _dbContext.WhatsAppSettings
                    .Where(x => x.BusinessId == dto.BusinessId && x.Id != existing.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
            }
            else
            {
                var s = new WhatsAppSettingEntity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    Provider = effectiveProvider,
                    ApiUrl = (dto.ApiUrl ?? string.Empty).Trim(),
                    ApiKey = (dto.ApiKey ?? string.Empty).Trim(),
                    SenderDisplayName = string.IsNullOrWhiteSpace(dto.SenderDisplayName) ? null : dto.SenderDisplayName.Trim(),
                    WabaId = string.IsNullOrWhiteSpace(dto.WabaId) ? null : dto.WabaId.Trim(),
                    WebhookSecret = string.IsNullOrWhiteSpace(dto.WebhookSecret) ? null : dto.WebhookSecret.Trim(),
                    WebhookVerifyToken = string.IsNullOrWhiteSpace(dto.WebhookVerifyToken) ? null : dto.WebhookVerifyToken.Trim(),
                    WebhookCallbackUrl = string.IsNullOrWhiteSpace(dto.WebhookCallbackUrl) ? null : dto.WebhookCallbackUrl.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = now
                };
                await _dbContext.WhatsAppSettings.AddAsync(s);
            }

            await _dbContext.SaveChangesAsync();

            // ── Numbers live in WhatsAppPhoneNumbers ─────────────────────────────
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumberId) &&
                !string.IsNullOrWhiteSpace(dto.WhatsAppBusinessNumber))
            {
                var pnid = dto.PhoneNumberId.Trim();
                var waNum = dto.WhatsAppBusinessNumber.Trim();
                var display = string.IsNullOrWhiteSpace(dto.SenderDisplayName) ? null : dto.SenderDisplayName!.Trim();

                // Upsert by business+provider+phoneId
                var providerFilter = effectiveProvider.ToLowerInvariant();

                var numberRow = await _dbContext.WhatsAppPhoneNumbers.FirstOrDefaultAsync(x =>
                    x.BusinessId == dto.BusinessId &&
                    x.Provider.ToLower() == providerFilter &&
                    x.PhoneNumberId == pnid);

                if (numberRow == null)
                {
                    numberRow = new WhatsAppPhoneNumber
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = dto.BusinessId,
                        Provider = effectiveProvider,
                        PhoneNumberId = pnid,
                        WhatsAppBusinessNumber = waNum,
                        SenderDisplayName = display,
                        IsActive = true,
                        IsDefault = true,
                        CreatedAt = now
                    };
                    _dbContext.WhatsAppPhoneNumbers.Add(numberRow);
                }
                else
                {
                    numberRow.WhatsAppBusinessNumber = waNum;
                    numberRow.SenderDisplayName = display;
                    numberRow.Provider = effectiveProvider; // ensure matches setting
                    numberRow.IsActive = true;
                    numberRow.IsDefault = true;
                    numberRow.UpdatedAt = now;
                }

                await _dbContext.SaveChangesAsync();

                // Ensure single default per business+provider
                await _dbContext.WhatsAppPhoneNumbers
                    .Where(x => x.BusinessId == dto.BusinessId &&
                                x.Provider.ToLower() == providerFilter &&
                                x.Id != numberRow.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));
            }
            // ─────────────────────────────────────────────────────────────────────

            await tx.CommitAsync();
        }

        public async Task<WhatsAppSettingsDto?> GetSettingsByBusinessIdAsync(Guid businessId)
        {
            // 1. Get the Settings (User's configuration)
            var setting = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Include(s => s.WhatsAppBusinessNumbers) // Load connected numbers via FK
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();

            if (setting == null) return null;

            // 2. Try to pick the best phone number from the navigation property (Strict FK match)
            var phone = setting.WhatsAppBusinessNumbers
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsDefault)
                .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .FirstOrDefault();

            // 3. Fallback: If FK didn't yield a number (e.g. Provider mismatch in DB due to legacy data), 
            // query WhatsAppPhoneNumbers table directly by BusinessId.
            if (phone == null)
            {
                phone = await _dbContext.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(p => p.BusinessId == businessId && p.IsActive)
                    .OrderByDescending(p => p.IsDefault)
                    .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            return new WhatsAppSettingsDto
            {
                BusinessId = setting.BusinessId,
                Provider = setting.Provider,
                ApiUrl = setting.ApiUrl,
                ApiKey = setting.ApiKey,
                WabaId = setting.WabaId,
                PhoneNumberId = phone?.PhoneNumberId,
                WhatsAppBusinessNumber = phone?.WhatsAppBusinessNumber,
                SenderDisplayName = phone?.SenderDisplayName
            };
        }

        public async Task<bool> DeleteSettingsAsync(Guid businessId, CancellationToken ct = default)
        {
            var all = await _dbContext.WhatsAppSettings
                .Where(x => x.BusinessId == businessId)
                .ToListAsync(ct);

            if (all.Count == 0) return false;

            _dbContext.WhatsAppSettings.RemoveRange(all);
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Provider-aware test connection. Returns a short message (✅/❌ …).
        /// The controller may convert non-✅ messages to 400, etc.
        /// </summary>
        public async Task<string> TestConnectionAsync(SaveWhatsAppSettingDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Provider))
                throw new ArgumentException("Provider is required.");

            var provider = dto.Provider.Trim();
            var lower = provider.ToLowerInvariant();
            var baseUrl = (dto.ApiUrl ?? string.Empty).Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("ApiUrl is required.");

            var http = _httpClientFactory.CreateClient();

            // ----- Meta Cloud -----
            if (lower == "meta_cloud")
            {
                if (string.IsNullOrWhiteSpace(dto.ApiKey))
                    throw new ArgumentException("ApiKey is required for Meta Cloud.");
                if (string.IsNullOrWhiteSpace(dto.PhoneNumberId))
                    throw new ArgumentException("PhoneNumberId is required for Meta Cloud.");

                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", dto.ApiKey);

                var url = $"{baseUrl}/{dto.PhoneNumberId}";
                var res = await http.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return $"❌ Meta Cloud test failed ({(int)res.StatusCode}). Body: {body}";

                return "✅ Meta Cloud token & phone number ID are valid.";
            }

            // ----- Pinnacle -----
            if (lower == "pinnacle")
            {
                if (string.IsNullOrWhiteSpace(dto.ApiKey))
                    return "❌ API Key is required for Pinnacle.";

                var pathId =
                    !string.IsNullOrWhiteSpace(dto.PhoneNumberId) ? dto.PhoneNumberId!.Trim() :
                    !string.IsNullOrWhiteSpace(dto.WabaId) ? dto.WabaId!.Trim() :
                    null;

                if (string.IsNullOrWhiteSpace(pathId))
                    return "❌ Provide PhoneNumberId or WabaId for Pinnacle.";

                if (string.IsNullOrWhiteSpace(dto.WhatsAppBusinessNumber))
                    return "❌ WhatsApp Business Number is required for Pinnacle test.";

                var url = $"{baseUrl}/{pathId}/messages";
                var payload = new
                {
                    to = dto.WhatsAppBusinessNumber,
                    type = "text",
                    text = new { body = "Test message" },
                    messaging_product = "whatsapp"
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("apikey", dto.ApiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var res = await http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    if ((int)res.StatusCode == 401 || (int)res.StatusCode == 403)
                        return $"❌ Pinnacle rejected the API key for id '{pathId}'. Verify the key and id. Body: {body}";

                    return $"❌ Pinnacle test failed ({(int)res.StatusCode}). Body: {body}";
                }

                return "✅ Pinnacle API key and endpoint are reachable.";
            }

            return $"❌ Unsupported provider: {dto.Provider}";
        }

        public async Task<string?> GetSenderNumberAsync(Guid businessId)
        {
            var providerRaw = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .Select(s => s.Provider)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(providerRaw))
                return null;

            var provider = providerRaw.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            var providerLc = provider.ToLowerInvariant();

            var number = await _dbContext.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == businessId &&
                            n.IsActive &&
                            n.Provider.ToLower() == providerLc)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .Select(n => n.WhatsAppBusinessNumber)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(number))
                return number;

            number = await _dbContext.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == businessId && n.IsActive)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .Select(n => n.WhatsAppBusinessNumber)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(number) ? null : number;
        }

        public async Task<string> GetCallbackUrlAsync(Guid businessId, string appBaseUrl)
        {
            var s = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive);

            if (!string.IsNullOrWhiteSpace(s?.WebhookCallbackUrl))
                return s!.WebhookCallbackUrl!;

            // ✅ align with your Meta dashboard: POST /api/webhooks/whatsapp
            return $"{appBaseUrl.TrimEnd('/')}/api/webhooks/whatsapp";
        }

        public async Task<IReadOnlyList<WhatsAppSettingEntity>> GetAllForBusinessAsync(Guid businessId)
        {
            var items = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId)
                .Include(s => s.WhatsAppBusinessNumbers)
                .OrderBy(s => s.Provider)
                .ToListAsync();

            return items.AsReadOnly();
        }

        public async Task<WhatsAppSettingEntity?> GetSettingsByBusinessIdAndProviderAsync(Guid businessId, string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return null;
            var prov = provider.Trim();

            return await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Provider.ToLower() == prov.ToLower());
        }

        // ---------------------------------------------------------------------
        // Connection Summary (Meta Health)
        // ---------------------------------------------------------------------

        public async Task<WhatsAppConnectionSummaryDto?> GetConnectionSummaryAsync(Guid businessId)
        {
            // 1. Fetch all active numbers for the business
            var allNumbers = await _dbContext.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == businessId && n.IsActive)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .ToListAsync();

            if (!allNumbers.Any()) return null;

            // 2. Use the primary (default or most recent) for the health summary
            var phoneRow = allNumbers.First();

            return new WhatsAppConnectionSummaryDto
            {
                BusinessId = phoneRow.BusinessId,
                PhoneNumberId = phoneRow.PhoneNumberId,
                WhatsAppBusinessNumber = phoneRow.WhatsAppBusinessNumber,
                WhatsAppBusinessNumbers = allNumbers.Select(x => x.WhatsAppBusinessNumber).Where(x => x != null).Cast<string>().ToList(),
                VerifiedName = phoneRow.VerifiedName,
                QualityRating = phoneRow.QualityRating,
                Status = phoneRow.Status,
                NameStatus = phoneRow.NameStatus,
                MessagingLimitTier = phoneRow.MessagingLimitTier,
                LastUpdated = phoneRow.ConnectionDataUpdatedAt
            };
        }

        public async Task<WhatsAppConnectionSummaryDto> RefreshConnectionSummaryAsync(Guid businessId)
        {
            // 1. Resolve Settings (for Token) & Number (for ID)
            var setting = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();

            if (setting == null)
                throw new InvalidOperationException("No active WhatsApp settings found.");

            // Only supported for Meta_Cloud (Pinnacle doesn't expose this Graph endpoint directly usually, 
            // or uses a different one. We'll support Meta_Cloud first as requested).
            if (!setting.Provider.Equals("META_CLOUD", StringComparison.OrdinalIgnoreCase))
            {
                // For Pinnacle, we might just return local data or throw not supported.
                // Assuming currently we focused on Meta Graph call.
                // If the user uses Pinnacle, we might need a specific partner API. 
                // For now, fail safe or return local.
                // Let's throw to be explicit if they try to "Refresh" on non-Meta.
                // Or better: just checking if we can do it. User asked for Graph call.
            }

            // We need the phone number ID to query graph
            var phoneRow = await _dbContext.WhatsAppPhoneNumbers
                .FirstOrDefaultAsync(n => n.BusinessId == businessId && n.IsActive && n.IsDefault);

            // Fallback to any active if no default
            if (phoneRow == null)
            {
                 phoneRow = await _dbContext.WhatsAppPhoneNumbers
                .Where(n => n.BusinessId == businessId && n.IsActive)
                .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .FirstOrDefaultAsync();
            }

            if (phoneRow == null)
                throw new InvalidOperationException("No active number found to refresh.");

            // 2. Call Graph API
            // GET /{PhoneId}?fields=verified_name,status,quality_rating,messaging_limit_tier
            if (string.IsNullOrWhiteSpace(setting.ApiKey))
                throw new InvalidOperationException("Missing API Key (Access Token) for Meta Cloud.");

            var http = _httpClientFactory.CreateClient();
            var baseUrl = string.IsNullOrWhiteSpace(setting.ApiUrl) 
                ? "https://graph.facebook.com/v21.0" 
                : setting.ApiUrl.TrimEnd('/');

            var url = $"{baseUrl}/{phoneRow.PhoneNumberId}?fields=verified_name,status,quality_rating,messaging_limit_tier,name_status&access_token={setting.ApiKey}";
            
            var res = await http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"Meta Graph Error {(int)res.StatusCode}: {body}");
            }

            // 3. Parse Response
            // { "verified_name": "...", "status": "CONNECTED", "quality_rating": "GREEN", "messaging_limit_tier": "TIER_1K", "id": "..." }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // 4. Update DB
            phoneRow.VerifiedName = root.TryGetProperty("verified_name", out var v) ? v.GetString() : null;
            phoneRow.Status = root.TryGetProperty("status", out var s) ? s.GetString()?.ToUpperInvariant() : null;
            phoneRow.QualityRating = root.TryGetProperty("quality_rating", out var q) ? q.GetString()?.ToUpperInvariant() : null;
            phoneRow.MessagingLimitTier = root.TryGetProperty("messaging_limit_tier", out var m) ? m.GetString()?.ToUpperInvariant() : null;
            phoneRow.NameStatus = root.TryGetProperty("name_status", out var ns) ? ns.GetString()?.ToUpperInvariant() : null;
            phoneRow.ConnectionDataUpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // 5. Fetch all active numbers for the business to include in DTO
            var allNumbers = await _dbContext.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == businessId && n.IsActive)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .ToListAsync();

            // 6. Return updated DTO
            return new WhatsAppConnectionSummaryDto
            {
                BusinessId = phoneRow.BusinessId,
                PhoneNumberId = phoneRow.PhoneNumberId,
                WhatsAppBusinessNumber = phoneRow.WhatsAppBusinessNumber,
                WhatsAppBusinessNumbers = allNumbers.Select(x => x.WhatsAppBusinessNumber).Where(x => x != null).Cast<string>().ToList(),
                VerifiedName = phoneRow.VerifiedName,
                QualityRating = phoneRow.QualityRating,
                Status = phoneRow.Status,
                NameStatus = phoneRow.NameStatus,
                MessagingLimitTier = phoneRow.MessagingLimitTier,
                LastUpdated = phoneRow.ConnectionDataUpdatedAt
            };
        }
    }
}
