// xbytechat.api/WhatsAppSettings/Providers/PinnacleTemplateCatalogProvider.cs
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using xbytechat.api.WhatsAppSettings.Abstractions;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;
using TemplateCatalogItem = xbytechat.api.WhatsAppSettings.Abstractions.TemplateCatalogItem;

namespace xbytechat.api.WhatsAppSettings.Providers
{
    // Make sure your DI registers this as IPinnacleTemplateCatalogProvider *and* ITemplateCatalogProvider if needed.
    public sealed class PinnacleTemplateCatalogProvider : IPinnacleTemplateCatalogProvider
    {
        private readonly HttpClient _http;
        private readonly ILogger<PinnacleTemplateCatalogProvider> _log;
        private readonly AppDbContext _context;

        public PinnacleTemplateCatalogProvider(
            HttpClient http,
            ILogger<PinnacleTemplateCatalogProvider> log,
            AppDbContext context)
        {
            _http = http;
            _log = log;
            _context = context;
        }

        // === Interface method used by TemplateSyncService ===
        // TemplateSyncService expects _pinnacle.ListAsync(setting, ct). We delegate to the concrete method below.
        public Task<IReadOnlyList<TemplateCatalogItem>> ListAsync(
            WhatsAppSettingEntity setting,
            CancellationToken ct = default)
            => ListPinnacleAsync(setting, ct);

        // === Concrete implementation ===
        public async Task<IReadOnlyList<TemplateCatalogItem>> ListPinnacleAsync(
            WhatsAppSettingEntity setting,
            CancellationToken ct = default)
        {
            var items = new List<TemplateCatalogItem>();

            if (string.IsNullOrWhiteSpace(setting.ApiKey))
            {
                _log.LogWarning("Pinnacle: missing ApiKey for BusinessId {BusinessId}", setting.BusinessId);
                return items;
            }

            var baseUrl = (setting.ApiUrl ?? "https://partnersv1.pinbot.ai").TrimEnd('/');
            if (!baseUrl.EndsWith("/v3", System.StringComparison.OrdinalIgnoreCase))
                baseUrl += "/v3";

            var prov = (setting.Provider ?? "")
                .Trim().Replace("-", "_").Replace(" ", "_")
                .ToUpperInvariant();

            var pathId = !string.IsNullOrWhiteSpace(setting.WabaId)
                ? setting.WabaId!.Trim()
                : await _context.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(p => p.BusinessId == setting.BusinessId
                                && p.IsActive
                                && p.Provider.ToUpper() == prov)
                    .OrderByDescending(p => p.IsDefault)
                    .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .Select(p => p.PhoneNumberId)
                    .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(pathId))
            {
                _log.LogWarning("Pinnacle: missing WabaId/PhoneNumberId for BusinessId {BusinessId}", setting.BusinessId);
                return items;
            }

            _http.DefaultRequestHeaders.Remove("apikey");
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("apikey", setting.ApiKey);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", setting.ApiKey);

            string? nextUrl = $"{baseUrl}/{pathId}/message_templates?limit=100";

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                ct.ThrowIfCancellationRequested();

                HttpResponseMessage res;
                string json;
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                    res = await _http.SendAsync(req, ct);
                    json = await res.Content.ReadAsStringAsync(ct);
                }
                catch (System.Exception ex)
                {
                    _log.LogError(ex, "❌ Pinnacle HTTP error for {Url}", nextUrl);
                    break;
                }

                if (!res.IsSuccessStatusCode)
                {
                    _log.LogError("❌ Pinnacle list failed ({Status}): {Body}", (int)res.StatusCode, json);
                    break;
                }

                dynamic parsed;
                try { parsed = JsonConvert.DeserializeObject(json)!; }
                catch (System.Exception ex)
                {
                    _log.LogError(ex, "❌ Pinnacle JSON parse error");
                    break;
                }

                var collection = parsed?.data ?? parsed?.templates;
                if (collection == null)
                {
                    _log.LogInformation("Pinnacle: no data/templates array.");
                    break;
                }

                foreach (var tpl in collection)
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();

                        string name = tpl?.name?.ToString() ?? "";
                        string language = tpl?.language?.ToString() ?? "";
                        string status = (tpl?.status?.ToString() ?? "APPROVED").Trim().ToUpperInvariant();
                        string category = tpl?.category?.ToString() ?? "";
                        string? subCat = tpl?.sub_category?.ToString();
                        string externalId = tpl?.id?.ToString() ?? "";

                        if (status != "APPROVED" && status != "ACTIVE" && status != "PENDING_REVIEW" && status != "IN_REVIEW")
                            continue;

                        string body = "";
                        string headerKind = "none"; // text/image/video/document/none
                        var buttons = new List<ButtonMetadataDto>();

                        var components = tpl?.components;
                        if (components != null)
                        {
                            foreach (var c in components)
                            {
                                var type = c?.type?.ToString()?.ToUpperInvariant();

                                if (type == "BODY")
                                {
                                    body = c?.text?.ToString() ?? "";
                                }
                                else if (type == "HEADER")
                                {
                                    var fmt = c?.format?.ToString()?.ToUpperInvariant();
                                    headerKind = fmt switch
                                    {
                                        "TEXT" => "text",
                                        "IMAGE" => "image",
                                        "VIDEO" => "video",
                                        "DOCUMENT" => "document",
                                        _ => "none"
                                    };
                                }
                                else if (type == "BUTTONS" && c?.buttons != null)
                                {
                                    foreach (var b in c.buttons)
                                    {
                                        string btnType = b?.type?.ToString()?.ToUpperInvariant() ?? "";
                                        string text = b?.text?.ToString() ?? "";

                                        // Prefer provider index if present; fallback to list order.
                                        int index = 0;
                                        if (!int.TryParse(b?.index?.ToString(), out index))
                                            index = buttons.Count;

                                        string subType = btnType switch
                                        {
                                            "URL" => "url",
                                            "PHONE_NUMBER" => "voice_call",
                                            "QUICK_REPLY" => "quick_reply",
                                            "COPY_CODE" => "copy_code",
                                            "CATALOG" => "catalog",
                                            "FLOW" => "flow",
                                            "REMINDER" => "reminder",
                                            "ORDER_DETAILS" => "order_details",
                                            _ => "unknown"
                                        };
                                        if (subType == "unknown") continue;

                                        string? param =
                                            b?.url?.ToString()
                                            ?? b?.phone_number?.ToString()
                                            ?? b?.coupon_code?.ToString()
                                            ?? b?.flow_id?.ToString();

                                        buttons.Add(new ButtonMetadataDto
                                        {
                                            Text = text,
                                            Type = btnType,
                                            SubType = subType,
                                            Index = index,
                                            ParameterValue = param ?? ""
                                        });
                                    }
                                }
                            }
                        }

                        // PlaceholderCount is not used by sync; it recomputes from RawJson.
                        var raw = JsonConvert.SerializeObject(tpl);

                        items.Add(new TemplateCatalogItem(
                            Name: name,
                            Language: language,
                            Body: body,
                            PlaceholderCount: 0,                 // sync derives counts from RawJson
                            HasImageHeader: headerKind == "image",
                            Buttons: buttons,
                            Status: status,
                            Category: category,
                            ExternalId: externalId,
                            RawJson: raw,
                            SubCategory: subCat
                        ));
                    }
                    catch
                    {
                        // swallow a bad item and continue
                    }
                }

                // safe paging read
                try { nextUrl = parsed?.paging?.next?.ToString(); }
                catch { nextUrl = null; }
                if (string.IsNullOrWhiteSpace(nextUrl)) break;
            }

            return items;
        }

        public Task<TemplateCatalogItem?> GetByNamePinnacleAsync(
            WhatsAppSettingEntity setting,
            string templateName,
            CancellationToken ct = default)
            => Task.FromResult<TemplateCatalogItem?>(null); // not used in sync path
    }
}
