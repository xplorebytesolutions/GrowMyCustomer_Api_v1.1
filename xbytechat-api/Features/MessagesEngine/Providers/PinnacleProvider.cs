// 📄 File: Features/MessagesEngine/Providers/PinnacleProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.MessagesEngine.Abstractions;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.MessagesEngine.Providers
{
    public class PinnacleProvider : IWhatsAppProvider
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http;
        private readonly ILogger<PinnacleProvider> _logger;
        private readonly WhatsAppSettingEntity _setting;

        // optional per-send override (phone_number_id or wabaId path segment)
        private readonly string? _pathIdOverride;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public PinnacleProvider(
            AppDbContext db,
            HttpClient http,
            ILogger<PinnacleProvider> logger,
            WhatsAppSettingEntity setting,
            string? pathIdOverride = null)
        {
            _db = db;
            _http = http;
            _logger = logger;
            _setting = setting;
            _pathIdOverride = pathIdOverride;
        }

        /// <summary>
        /// Resolve the path identifier used by Pinnacle: prefer explicit override,
        /// then WabaId from settings, then PhoneNumberId from WhatsAppPhoneNumbers.
        /// </summary>
        private string? ResolvePathIdOrNull()
        {
            if (!string.IsNullOrWhiteSpace(_pathIdOverride))
                return _pathIdOverride;

            if (!string.IsNullOrWhiteSpace(_setting.WabaId))
                return _setting.WabaId;

            var providerKey = (_setting.Provider ?? string.Empty).Trim().ToLowerInvariant();

            // fallback to default active sender for this provider
            var phoneId = _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == _setting.BusinessId
                            && n.IsActive
                            && n.Provider.ToLower() == providerKey)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .Select(n => n.PhoneNumberId)
                .FirstOrDefault();

            return phoneId;
        }

        private string BuildBaseUrl()
        {
            var baseUrl = string.IsNullOrWhiteSpace(_setting.ApiUrl)
                ? "https://partnersv1.pinbot.ai"
                : _setting.ApiUrl.TrimEnd('/');

            if (!baseUrl.EndsWith("/v3", StringComparison.OrdinalIgnoreCase))
                baseUrl += "/v3";

            return baseUrl;
        }

        /// <summary>
        /// Build the final send URL. If pathId is already a full URL, use as-is (for tracking cases).
        /// Otherwise, compose the Pinnacle endpoint and ALWAYS append apikey in the query.
        /// </summary>
        private string BuildSendUrlWithApiKey(string pathId)
        {
            if (Uri.IsWellFormedUriString(pathId, UriKind.Absolute))
                return pathId;

            var baseUrl = BuildBaseUrl();
            var apiKey = _setting.ApiKey ?? string.Empty;
            return $"{baseUrl}/{pathId}/messages?apikey={Uri.EscapeDataString(apiKey)}";
        }

        private async Task<WaSendResult> PostAsync(object payload)
        {
            var pathId = ResolvePathIdOrNull();
            if (string.IsNullOrWhiteSpace(pathId))
            {
                const string err = "Pinnacle: Missing path id (need WabaId or PhoneNumberId/default sender).";
                _logger.LogError(err);
                return new WaSendResult(false, "Pinnacle", null, null, null, err);
            }

            if (string.IsNullOrWhiteSpace(_setting.ApiKey))
            {
                const string err = "Pinnacle: ApiKey is missing in WhatsApp settings.";
                _logger.LogError(err);
                return new WaSendResult(false, "Pinnacle", null, null, null, err);
            }

            var url = BuildSendUrlWithApiKey(pathId);
            var json = JsonSerializer.Serialize(payload, _jsonOpts);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // Put key in all common header places (some tenants validate one or the other)
            req.Headers.Remove("apikey");
            req.Headers.Remove("x-api-key");
            req.Headers.TryAddWithoutValidation("apikey", _setting.ApiKey);
            req.Headers.TryAddWithoutValidation("x-api-key", _setting.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Apikey", _setting.ApiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // trace (shortened key)
            _logger.LogInformation("Pinnacle POST {Url}", url);

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pinnacle send failed (HTTP {Status}): {Body}", (int)res.StatusCode, body);
                return new WaSendResult(false, "Pinnacle", null, res.StatusCode, body, res.ReasonPhrase);
            }

            string? id = TryGetPinnMessageId(body);
            return new WaSendResult(true, "Pinnacle", id, res.StatusCode, body, null);
        }

        public Task<WaSendResult> SendTextAsync(string to, string body)
            => PostAsync(new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { preview_url = false, body }
            });

        public Task<WaSendResult> SendTemplateAsync(string to, string templateName, string languageCode, IEnumerable<object> components)
        {
            components ??= Enumerable.Empty<object>();
            // Pinnacle expects language as-is (not { code: "xx_YY" })
            var langValue = languageCode;
            return PostAsync(new
            {
                messaging_product = "whatsapp",
                to,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = langValue,
                    components
                }
            });
        }

        private static string? TryGetPinnMessageId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("messages", out var msgs) &&
                    msgs.ValueKind == JsonValueKind.Array &&
                    msgs.GetArrayLength() > 0 &&
                    msgs[0].TryGetProperty("id", out var id0))
                    return id0.GetString();

                if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                {
                    if (msg.TryGetProperty("id", out var id1)) return id1.GetString();
                    if (msg.TryGetProperty("messageId", out var id2)) return id2.GetString();
                }

                if (root.TryGetProperty("message_id", out var id3)) return id3.GetString();
                if (root.TryGetProperty("messageId", out var id4)) return id4.GetString();
                if (root.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("messageId", out var id5)) return id5.GetString();
                if (root.TryGetProperty("id", out var idTop)) return idTop.GetString();
            }
            catch { /* ignore parse errors, return null */ }

            return null;
        }

        public Task<WaSendResult> SendInteractiveAsync(object fullPayload) => PostAsync(fullPayload);
    }
}



