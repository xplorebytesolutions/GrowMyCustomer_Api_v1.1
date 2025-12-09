// 📄 xbytechat_api/WhatsAppSettings/Services/WhatsAppSettingsService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        private readonly HttpClient _http;                    
        private readonly IHttpClientFactory _httpClientFactory;

        public WhatsAppSettingsService(
            AppDbContext dbContext,
            HttpClient http,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _http = http;
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

            await using var tx = await _dbContext.Database.BeginTransactionAsync();

            // Exact business + provider row
            var existing = await _dbContext.WhatsAppSettings
                .FirstOrDefaultAsync(x => x.BusinessId == dto.BusinessId &&
                                          x.Provider.ToLower() == providerCanon.ToLower());

            if (existing != null)
            {
                if (!string.Equals(existing.Provider, providerCanon, StringComparison.Ordinal))
                    existing.Provider = providerCanon;

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
                    Provider = providerCanon,
                    ApiUrl = (dto.ApiUrl ?? string.Empty).Trim(),
                    ApiKey = (dto.ApiKey ?? string.Empty).Trim(),
                    // Numbers are not stored here anymore
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
                var numberRow = await _dbContext.WhatsAppPhoneNumbers.FirstOrDefaultAsync(x =>
                    x.BusinessId == dto.BusinessId &&
                    x.Provider.ToLower() == providerCanon.ToLower() &&
                    x.PhoneNumberId == pnid);

                if (numberRow == null)
                {
                    numberRow = new WhatsAppPhoneNumber
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = dto.BusinessId,
                        Provider = providerCanon,
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
                    numberRow.IsActive = true;
                    numberRow.IsDefault = true;
                    numberRow.UpdatedAt = now;
                }

                await _dbContext.SaveChangesAsync();

                // Ensure single default per business+provider
                await _dbContext.WhatsAppPhoneNumbers
                    .Where(x => x.BusinessId == dto.BusinessId &&
                                x.Provider.ToLower() == providerCanon.ToLower() &&
                                x.Id != numberRow.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));
            }
            // ─────────────────────────────────────────────────────────────────────

            await tx.CommitAsync();
        }

        public async Task<WhatsAppSettingsDto> GetSettingsByBusinessIdAsync(Guid businessId)
        {
            var row = await (
                from s in _dbContext.WhatsAppSettings.AsNoTracking()
                where s.BusinessId == businessId && s.IsActive
                orderby (s.UpdatedAt ?? s.CreatedAt) descending
                select new
                {
                    s.BusinessId,
                    s.Provider,
                    s.ApiUrl,
                    s.ApiKey,
                    s.WabaId,
                    Phone = _dbContext.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(p => p.BusinessId == s.BusinessId
                                    && p.IsActive
                                    && p.Provider.ToLower() == s.Provider.ToLower())
                        .OrderByDescending(p => p.IsDefault)
                        .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                        .Select(p => new { p.PhoneNumberId, p.WhatsAppBusinessNumber })
                        .FirstOrDefault()
                }
            ).FirstOrDefaultAsync();

            if (row == null) return null;

            return new WhatsAppSettingsDto
            {
                BusinessId = row.BusinessId,
                Provider = row.Provider,
                ApiUrl = row.ApiUrl,
                ApiKey = row.ApiKey,
                WabaId = row.WabaId,
                PhoneNumberId = row.Phone?.PhoneNumberId,
                WhatsAppBusinessNumber = row.Phone?.WhatsAppBusinessNumber
            };
        }

        public async Task<bool> DeleteSettingsAsync(Guid businessId)
        {
            var setting = await _dbContext.WhatsAppSettings
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (setting == null) return false;

            _dbContext.WhatsAppSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();
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

            // normalize provider and baseUrl
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

            // ----- Pinnacle (formerly Pinbot) -----
            if (lower == "pinnacle")
            {
                if (string.IsNullOrWhiteSpace(dto.ApiKey))
                    return "❌ API Key is required for Pinnacle.";

                // Pinnacle requires either phone number id OR WABA id in the path
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

        // ✅ UPDATED: read from WhatsAppPhoneNumbers (default by provider), fallback to mirrored settings value
        public async Task<string?> GetSenderNumberAsync(Guid businessId)
        {
            // 1) Find this business's active provider
            var providerRaw = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .Select(s => s.Provider)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(providerRaw))
                return null;

            // Canonical provider ("META_CLOUD" | "PINNACLE")
            var provider = providerRaw.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            var providerLc = provider.ToLowerInvariant();

            // 2) Prefer the default active number for THAT provider
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

            // 3) Fallback: any active number for this business (regardless of provider)
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

            return $"{appBaseUrl.TrimEnd('/')}/api/webhookcallback";
        }

        public async Task<IReadOnlyList<WhatsAppSettingEntity>> GetAllForBusinessAsync(Guid businessId)
        {
            // Return all rows (one per provider) for this business + include numbers collection
            var items = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId)
                .Include(s => s.WhatsAppBusinessNumbers) // ✅ correct navigation include
                .OrderBy(s => s.Provider)
                .ToListAsync();

            return items.AsReadOnly();
        }

        public async Task<WhatsAppSettingEntity?> GetSettingsByBusinessIdAndProviderAsync(Guid businessId, string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return null;
            var prov = provider.Trim();

            // case-insensitive provider match
            return await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Provider.ToLower() == prov.ToLower());
        }
    }
}

