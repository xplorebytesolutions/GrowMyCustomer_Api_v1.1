using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.WhatsAppSettings.Models;
using xbytechat.api.Features.WhatsAppSettings.Services;
using xbytechat_api.WhatsAppSettings.Models;
using xbytechat.api.WhatsAppSettings.DTOs;


namespace xbytechat.api.Features.WhatsAppSettings.Services
{
    public sealed class WhatsAppPhoneNumberService : IWhatsAppPhoneNumberService
    {
        private readonly AppDbContext _db;

        public WhatsAppPhoneNumberService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<WhatsAppPhoneNumber>> ListAsync(
           Guid businessId,
           string provider,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentException("Provider is required.", nameof(provider));

            // Enforce your uppercase-only contract (no normalization here)
            if (provider is not "PINNACLE" and not "META_CLOUD")
                throw new ArgumentOutOfRangeException(nameof(provider),
                    "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");

            var list = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.Provider == provider) // exact, case-sensitive
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.WhatsAppBusinessNumber)
                .ToListAsync(ct);

            return list; // List<T> implements IReadOnlyList<T>
        }

        private static string NormalizeProvider(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Pinnacle";
            var s = raw.Trim();
            if (string.Equals(s, "Pinnacle", StringComparison.OrdinalIgnoreCase)) return "Pinnacle";
            if (string.Equals(s, "Meta_cloud", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "meta_cloud", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "meta", StringComparison.OrdinalIgnoreCase))
                return "Meta_cloud";
            return s;
        }

        public async Task<(int Added, int Updated, int Total)> SyncFromProviderAsync(
            Guid businessId,
            WhatsAppSettingsDto s,                 // DTO
            string provider,
            CancellationToken ct = default)
        {
            // Always honor the provider saved in DB (CAPS), fall back to the arg if needed
            var providerCanon = (s.Provider ?? provider ?? string.Empty)
                .Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();

            List<(string PhoneId, string Number, string? Label, string? Status)> list = providerCanon switch
            {
                "META_CLOUD" => await FetchFromMetaAsync(s, ct),
                "PINNACLE" => await FetchFromPinnacleAsync(s, ct),
                _ => new List<(string PhoneId, string Number, string? Label, string? Status)>()
            };

            var added = 0;
            var updated = 0;

            foreach (var (phoneId, number, label, status) in list)
            {
                var existing = await _db.WhatsAppPhoneNumbers
                    .FirstOrDefaultAsync(x =>
                        x.BusinessId == businessId &&
                        x.Provider == providerCanon &&
                        x.PhoneNumberId == phoneId, ct);

                if (existing is null)
                {
                    _db.WhatsAppPhoneNumbers.Add(new WhatsAppPhoneNumber
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Provider = providerCanon,                 // segregate by provider
                        PhoneNumberId = phoneId,
                        WhatsAppBusinessNumber = number,
                        SenderDisplayName = string.IsNullOrWhiteSpace(label) ? null : label,
                        Status = status,
                        IsActive = true,
                        IsDefault = false
                    });
                    added++;
                }
                else
                {
                    existing.WhatsAppBusinessNumber = number;
                    if (!string.IsNullOrWhiteSpace(label))
                        existing.SenderDisplayName = label;
                    existing.Status = status;
                    existing.IsActive = true;
                    updated++;
                }
            }

            await _db.SaveChangesAsync(ct);
            return (added, updated, list.Count);
        }

        private async Task<List<(string PhoneId, string Number, string? Label, string? Status)>> FetchFromMetaAsync(
            WhatsAppSettingsDto s,
            CancellationToken ct)
        {
            var list = new List<(string, string, string?, string?)>();

            if (string.IsNullOrWhiteSpace(s.ApiKey) || string.IsNullOrWhiteSpace(s.WabaId))
                return list;

            var baseUrl = string.IsNullOrWhiteSpace(s.ApiUrl)
                ? "https://graph.facebook.com/v22.0"
                : s.ApiUrl.TrimEnd('/');

            var url = $"{baseUrl}/{s.WabaId}/phone_numbers?fields=id,display_phone_number,verified_name,status&limit=100";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            var res = await http.GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return list;

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var data = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d
                : default;

            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in data.EnumerateArray())
                {
                    var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString()
                             : e.TryGetProperty("phone_number_id", out var pid) ? pid.GetString()
                             : null;

                    var num = e.TryGetProperty("display_phone_number", out var dnum) ? dnum.GetString()
                             : e.TryGetProperty("number", out var numEl) ? numEl.GetString()
                             : null;

                    var name = e.TryGetProperty("verified_name", out var vn) ? vn.GetString()
                             : e.TryGetProperty("name", out var nm) ? nm.GetString()
                             : null;

                    var status = e.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(num))
                        list.Add((id!, num!, name, status));
                }
            }

            return list;
        }

        private async Task<List<(string PhoneId, string Number, string? Label, string? Status)>> FetchFromPinnacleAsync(
            WhatsAppSettingsDto s,
            CancellationToken ct)
        {
            var list = new List<(string, string, string?, string?)>();

            if (string.IsNullOrWhiteSpace(s.ApiKey))
                return list;

            // Base URL and /v3 suffix
            var baseUrl = string.IsNullOrWhiteSpace(s.ApiUrl)
                ? "https://partnersv1.pinbot.ai"
                : s.ApiUrl.TrimEnd('/');

            if (!baseUrl.EndsWith("/v3", StringComparison.OrdinalIgnoreCase))
                baseUrl += "/v3";

            // Prefer WABA, fall back to PhoneNumberId
            var pathId = !string.IsNullOrWhiteSpace(s.WabaId) ? s.WabaId!.Trim()
                       : !string.IsNullOrWhiteSpace(s.PhoneNumberId) ? s.PhoneNumberId!.Trim()
                       : null;

            if (string.IsNullOrWhiteSpace(pathId))
                return list;

            var url = $"{baseUrl}/{pathId}/phone_numbers";

            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("apikey", s.ApiKey);

            var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return list;

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var data = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d
                : default;

            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in data.EnumerateArray())
                {
                    var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString()
                             : e.TryGetProperty("phone_number_id", out var pid) ? pid.GetString()
                             : null;

                    var num = e.TryGetProperty("display_phone_number", out var dnum) ? dnum.GetString()
                             : e.TryGetProperty("msisdn", out var msisdn) ? msisdn.GetString()
                             : e.TryGetProperty("number", out var numEl) ? numEl.GetString()
                             : null;

                    var name = e.TryGetProperty("verified_name", out var vn) ? vn.GetString()
                             : e.TryGetProperty("name", out var nm) ? nm.GetString()
                             : null;

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(num))
                        list.Add((id!, num!, name, null));
                }
            }

            return list;
        }


        private static string NormalizeMetaBase(string? apiUrl)
                        => string.IsNullOrWhiteSpace(apiUrl) ? "https://graph.facebook.com/v22.0" : apiUrl.Trim();

        private async Task<List<(string PhoneId, string Number, string? Label, string? Status)>> FetchFromMetaAsync(
            WhatsAppSettingEntity s, CancellationToken ct)
        {
            var baseUrl = NormalizeMetaBase(s.ApiUrl);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", s.ApiKey!.Trim());

            var list = new List<(string, string, string?, string?)>();
            string? after = null;

            do
            {
                var url = $"{baseUrl}/{s.WabaId}/phone_numbers?fields=id,display_phone_number,verified_name,status";
                if (!string.IsNullOrEmpty(after)) url += $"&after={WebUtility.UrlEncode(after)}";

                var json = await http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in data.EnumerateArray())
                    {
                        var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var num = e.TryGetProperty("display_phone_number", out var nEl) ? nEl.GetString() : null;
                        var name = e.TryGetProperty("verified_name", out var lEl) ? lEl.GetString() : null;
                        var status = e.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(num))
                            list.Add((id!, num!, name, status));
                    }
                }

                after = root.TryGetProperty("paging", out var p)
                     && p.TryGetProperty("cursors", out var c)
                     && c.TryGetProperty("after", out var a)
                     && a.ValueKind == JsonValueKind.String ? a.GetString() : null;

            } while (!string.IsNullOrEmpty(after));

            return list;
        }


        public async Task<WhatsAppPhoneNumber> UpsertAsync(
              Guid businessId,
              string provider, string phoneNumberId,
              string whatsAppBusinessNumber, string? senderDisplayName,
              bool? isActive = null,
         bool? isDefault = null)
        {
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("provider is required");
            if (string.IsNullOrWhiteSpace(phoneNumberId)) throw new ArgumentException("phoneNumberId is required");
            if (string.IsNullOrWhiteSpace(whatsAppBusinessNumber)) throw new ArgumentException("whatsAppBusinessNumber is required");

            // Canonical provider: "PINNACLE" | "META_CLOUD"
            var prov = provider.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            var pnid = phoneNumberId.Trim();
            var waNum = whatsAppBusinessNumber.Trim();
            var disp = string.IsNullOrWhiteSpace(senderDisplayName) ? null : senderDisplayName!.Trim();

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync();

            // 1) Ensure parent settings row exists for (BusinessId, Provider) [case-insensitive]
            var setting = await _db.WhatsAppSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Provider.ToLower() == prov.ToLower());

            if (setting == null)
            {
                setting = new WhatsAppSettingEntity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Provider = prov,
                    ApiUrl = string.Empty,
                    ApiKey = string.Empty,
                    IsActive = true,
                    CreatedAt = now
                };
                _db.WhatsAppSettings.Add(setting);
                await _db.SaveChangesAsync();
            }
            else if (!string.Equals(setting.Provider, prov, StringComparison.Ordinal))
            {
                // normalize stored provider casing
                setting.Provider = prov;
                setting.UpdatedAt = now;
                await _db.SaveChangesAsync();
            }

            var providerForChild = setting.Provider; // exact value used for children

            // 2) Upsert phone number in WhatsAppPhoneNumbers
            var entity = await _db.WhatsAppPhoneNumbers.FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.Provider.ToLower() == providerForChild.ToLower() &&
                x.PhoneNumberId == pnid);

            if (entity == null)
            {
                entity = new WhatsAppPhoneNumber
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Provider = providerForChild,
                    PhoneNumberId = pnid,
                    WhatsAppBusinessNumber = waNum,
                    SenderDisplayName = disp,
                    IsActive = isActive ?? true,
                    IsDefault = isDefault ?? false,
                    CreatedAt = now
                };
                _db.WhatsAppPhoneNumbers.Add(entity);
            }
            else
            {
                entity.WhatsAppBusinessNumber = waNum;
                entity.SenderDisplayName = disp;
                if (isActive.HasValue) entity.IsActive = isActive.Value;
                if (isDefault.HasValue) entity.IsDefault = isDefault.Value;
                entity.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();

            // 3) Enforce a single default per (BusinessId, Provider)
            if (isDefault == true || entity.IsDefault)
            {
                await _db.WhatsAppPhoneNumbers
                    .Where(x => x.BusinessId == businessId &&
                                x.Provider.ToLower() == providerForChild.ToLower() &&
                                x.Id != entity.Id)
                    .ExecuteUpdateAsync(up => up.SetProperty(p => p.IsDefault, false));

                // We no longer mirror to WhatsAppSettings because those columns were removed.
                // If you ever re-introduce them, you can set:
                // setting.PhoneNumberId = entity.PhoneNumberId;
                // setting.WhatsAppBusinessNumber = entity.WhatsAppBusinessNumber;
                setting.UpdatedAt = now;
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid businessId, string provider, Guid id)
        {
            var prov = provider?.Trim() ?? string.Empty;

            var entity = await _db.WhatsAppPhoneNumbers
                .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && x.Provider.ToLower() == prov.ToLower());

            if (entity == null) return false;

            _db.WhatsAppPhoneNumbers.Remove(entity);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDefaultAsync(Guid businessId, string provider, Guid id)
        {
            var prov = provider?.Trim() ?? string.Empty;

            await _db.Database.BeginTransactionAsync();
            try
            {
                // ensure target exists and belongs to (business, provider)
                var target = await _db.WhatsAppPhoneNumbers
                    .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && x.Provider.ToLower() == prov.ToLower());
                if (target == null) return false;

                await _db.WhatsAppPhoneNumbers
                    .Where(x => x.BusinessId == businessId && x.Provider.ToLower() == prov.ToLower())
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));

                target.IsDefault = true;
                target.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await _db.Database.CommitTransactionAsync();
                return true;
            }
            catch
            {
                await _db.Database.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<WhatsAppPhoneNumber?> FindAsync(Guid businessId, string provider, string phoneNumberId)
        {
            var prov = provider?.Trim() ?? string.Empty;

            return await _db.WhatsAppPhoneNumbers
                .FirstOrDefaultAsync(x =>
                    x.BusinessId == businessId &&
                    x.Provider.ToLower() == prov.ToLower() &&
                    x.PhoneNumberId == phoneNumberId);
        }

        public async Task<WhatsAppPhoneNumber?> GetDefaultAsync(Guid businessId, string provider)
        {
            var prov = provider?.Trim() ?? string.Empty;

            // covered by partial unique index: at most one IsDefault per (biz, provider)
            return await _db.WhatsAppPhoneNumbers
                .FirstOrDefaultAsync(x =>
                    x.BusinessId == businessId &&
                    x.Provider.ToLower() == prov.ToLower() &&
                    x.IsDefault);
        }
    }
}
