// 📄 File: Features/MessagesEngine/Providers/MetaCloudProvider.cs
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // for AsNoTracking()
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.MessagesEngine.Abstractions;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.MessagesEngine.Providers
{
    public class MetaCloudProvider : IWhatsAppProvider
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http;
        private readonly ILogger<MetaCloudProvider> _logger;
        private readonly WhatsAppSettingEntity _setting;

        // Optional per-send override injected by the factory/engine (safe to leave null)
        private readonly string? _phoneNumberIdOverride;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            // camelCase ensures typed models serialize as Meta expects (e.g. `language.code`)
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public MetaCloudProvider(
            AppDbContext db,
            HttpClient http,
            ILogger<MetaCloudProvider> logger,
            WhatsAppSettingEntity setting,
            string? phoneNumberIdOverride = null)
        {
            _db = db;
            _http = http;
            _logger = logger;
            _setting = setting;
            _phoneNumberIdOverride = phoneNumberIdOverride;
        }

        /// <summary>
        /// Resolve the phone_number_id to use for Meta Cloud:
        /// 1) explicit override if provided; else
        /// 2) default active number from WhatsAppPhoneNumbers for this business+provider.
        /// </summary>
        private string? ResolvePhoneNumberId()
        {
            if (!string.IsNullOrWhiteSpace(_phoneNumberIdOverride))
                return _phoneNumberIdOverride;

            var providerKey = (_setting.Provider ?? string.Empty).Trim().ToLowerInvariant();

            var pnid = _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.BusinessId == _setting.BusinessId
                            && n.IsActive
                            && n.Provider.ToLower() == providerKey)
                .OrderByDescending(n => n.IsDefault)
                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                .Select(n => n.PhoneNumberId)
                .FirstOrDefault();

            return pnid;
        }

        private string BuildUrl()
        {
            var baseUrl = string.IsNullOrWhiteSpace(_setting.ApiUrl)
                ? "https://graph.facebook.com/v22.0"
                : _setting.ApiUrl.TrimEnd('/');

            var phoneNumberId = ResolvePhoneNumberId();
            if (string.IsNullOrWhiteSpace(phoneNumberId))
            {
                _logger.LogError("MetaCloudProvider: PhoneNumberId is missing for BusinessId {BusinessId}", _setting.BusinessId);
                return $"{baseUrl}/-/messages"; // inert path; request will fail with clear logs
            }

            return $"{baseUrl}/{phoneNumberId}/messages";
        }

        /// <summary>
        /// Recursively remove any `$type` discriminator properties from a JsonNode tree.
        /// Meta rejects unknown keys like `$type` inside template.components[*].
        /// </summary>
        private static void StripDollarType(JsonNode? node)
        {
            if (node is null) return;

            if (node is JsonObject obj)
            {
                if (obj.ContainsKey("$type"))
                    obj.Remove("$type");

                // safe enumeration snapshot
                foreach (var kv in obj.ToList())
                    StripDollarType(kv.Value);
            }
            else if (node is JsonArray arr)
            {
                foreach (var child in arr)
                    StripDollarType(child);
            }
        }

        private async Task<WaSendResult> PostAsync(object payload)
        {
            var url = BuildUrl();

            // Sanitize payload to remove any $type injected by polymorphic models
            var node = JsonSerializer.SerializeToNode(payload, _jsonOpts);
            StripDollarType(node);
            var json = node?.ToJsonString(_jsonOpts) ?? "{}";

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(_setting.ApiKey))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _setting.ApiKey);
            }
            else
            {
                _logger.LogWarning("MetaCloudProvider: ApiToken is empty for BusinessId {BusinessId}", _setting.BusinessId);
            }

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("MetaCloud send failed (HTTP {Status}): {Body}", (int)res.StatusCode, body);
                return new WaSendResult(false, "MetaCloud", null, res.StatusCode, body, res.ReasonPhrase);
            }

            string? id = null;
            try
            {
                var root = JsonNode.Parse(body);
                id = root?["messages"]?[0]?["id"]?.GetValue<string>();
            }
            catch { /* keep raw */ }

            return new WaSendResult(true, "MetaCloud", id, res.StatusCode, body, null);
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
            => PostAsync(new
            {
                messaging_product = "whatsapp",
                to,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = languageCode }, // Meta expects { "code": "en_US" }
                    components = components ?? System.Linq.Enumerable.Empty<object>()
                }
            });

        public Task<WaSendResult> SendInteractiveAsync(object fullPayload)
            => PostAsync(fullPayload);
    }
}
