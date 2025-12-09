using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using xbytechat.api;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Shared;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat.api.WhatsAppSettings.Helpers;
using DTO = xbytechat.api.WhatsAppSettings.DTOs;
namespace xbytechat_api.WhatsAppSettings.Services
{

    public class WhatsAppTemplateFetcherService : IWhatsAppTemplateFetcherService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppTemplateFetcherService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public WhatsAppTemplateFetcherService(AppDbContext dbContext, HttpClient httpClient, ILogger<WhatsAppTemplateFetcherService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<List<TemplateMetadataDto>> FetchTemplatesAsync(Guid businessId)
        {
            var templates = new List<TemplateMetadataDto>();

            var setting = await _dbContext.WhatsAppSettings
                .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive);

            if (setting == null)
            {
                _logger.LogWarning("WhatsApp Settings not found for BusinessId: {BusinessId}", businessId);
                return templates;
            }

            // Canonical provider
            var provider = (setting.Provider ?? string.Empty)
                .Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();

            // Common base url normalization
            var rawBase = (setting.ApiUrl ?? string.Empty).Trim().TrimEnd('/');

            try
            {
                // ==================== META_CLOUD ====================
                if (provider == "META_CLOUD")
                {
                    var baseUrl = string.IsNullOrWhiteSpace(rawBase)
                        ? "https://graph.facebook.com/v22.0"
                        : rawBase;

                    if (string.IsNullOrWhiteSpace(setting.ApiKey) || string.IsNullOrWhiteSpace(setting.WabaId))
                    {
                        _logger.LogWarning("Missing Meta ApiKey or WABA ID for BusinessId: {BusinessId}", businessId);
                        return templates;
                    }

                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", setting.ApiKey);

                    var nextUrl = $"{baseUrl}/{setting.WabaId!.Trim()}/message_templates?limit=100";

                    while (!string.IsNullOrWhiteSpace(nextUrl))
                    {
                        var response = await _httpClient.GetAsync(nextUrl);
                        var json = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("📦 Meta Template API Raw JSON for {BusinessId}:\n{Json}", setting.BusinessId, json);

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("❌ Failed to fetch templates from Meta: {Response}", json);
                            break;
                        }

                        dynamic parsed = JsonConvert.DeserializeObject(json);
                        templates.AddRange(ParseTemplatesFromMetaLikePayload(parsed));
                        nextUrl = parsed?.paging?.next?.ToString();
                    }

                    return templates;
                }

                // ==================== PINNACLE ====================
                if (provider == "PINNACLE")
                {
                    // Ensure base has /v3
                    var baseUrl = string.IsNullOrWhiteSpace(rawBase) ? "https://partnersv1.pinbot.ai" : rawBase;
                    if (!baseUrl.EndsWith("/v3", StringComparison.OrdinalIgnoreCase)) baseUrl += "/v3";

                    if (string.IsNullOrWhiteSpace(setting.ApiKey))
                    {
                        _logger.LogWarning("Pinnacle ApiKey missing for BusinessId: {BusinessId}", businessId);
                        return templates;
                    }

                    // Prefer WABA; otherwise use the DEFAULT phoneNumberId for this business+provider
                    var pathId = !string.IsNullOrWhiteSpace(setting.WabaId)
                        ? setting.WabaId!.Trim()
                        : await _dbContext.WhatsAppPhoneNumbers
                            .AsNoTracking()
                            .Where(p => p.BusinessId == businessId
                                        && p.IsActive
                                        && p.Provider.ToUpper() == "PINNACLE")
                            .OrderByDescending(p => p.IsDefault)
                            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                            .Select(p => p.PhoneNumberId)
                            .FirstOrDefaultAsync();

                    if (string.IsNullOrWhiteSpace(pathId))
                    {
                        _logger.LogWarning("Pinnacle path id missing (WabaId or default PhoneNumberId) for BusinessId: {BusinessId}", businessId);
                        return templates;
                    }

                    // Headers Pinnacle commonly accepts
                    _httpClient.DefaultRequestHeaders.Remove("apikey");
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("apikey", setting.ApiKey);

                    var nextUrl = $"{baseUrl}/{pathId}/message_templates?limit=100";

                    while (!string.IsNullOrWhiteSpace(nextUrl))
                    {
                        var response = await _httpClient.GetAsync(nextUrl);
                        var json = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("📦 Pinnacle Template API Raw JSON for {BusinessId}:\n{Json}", setting.BusinessId, json);

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("❌ Failed to fetch templates from Pinnacle: {Response}", json);
                            break;
                        }

                        // Many BSPs mirror Meta’s shape; also accept { data: [...] }
                        dynamic parsed = JsonConvert.DeserializeObject(json);
                        templates.AddRange(ParseTemplatesFromMetaLikePayload(parsed));
                        nextUrl = parsed?.paging?.next?.ToString();
                        if (nextUrl == null) break; // if their API doesn’t paginate
                    }

                    return templates;
                }

                // ==================== UNKNOWN ====================
                _logger.LogInformation("Provider {Provider} does not support listing via API in this build.", provider);
                return templates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching WhatsApp templates for provider {Provider}.", provider);
                return templates;
            }
        }

        private static IEnumerable<TemplateMetadataDto> ParseTemplatesFromMetaLikePayload(dynamic parsed)
        {
            var list = new List<TemplateMetadataDto>();
            if (parsed == null) return list;

            // Prefer parsed.data; fall back to parsed.templates
            var collection = parsed.data ?? parsed.templates;
            if (collection == null) return list;

            foreach (var tpl in collection)
            {
                string name = tpl.name?.ToString() ?? "";
                string language = tpl.language?.ToString() ?? "en_US";
                string body = "";
                bool hasImageHeader = false;
                var buttons = new List<ButtonMetadataDto>();

                // components may be null for some BSPs
                var components = tpl.components;
                if (components != null)
                {
                    foreach (var component in components)
                    {
                        string type = component.type?.ToString()?.ToUpperInvariant();

                        if (type == "BODY")
                            body = component.text?.ToString() ?? "";

                        if (type == "HEADER" && (component.format?.ToString()?.ToUpperInvariant() == "IMAGE"))
                            hasImageHeader = true;

                        if (type == "BUTTONS" && component.buttons != null)
                        {
                            foreach (var button in component.buttons)
                            {
                                try
                                {
                                    string btnType = button.type?.ToString()?.ToUpperInvariant() ?? "";
                                    string text = button.text?.ToString() ?? "";
                                    int index = buttons.Count;

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

                                    string? paramValue =
                                        button.url != null ? button.url.ToString() :
                                        button.phone_number != null ? button.phone_number.ToString() :
                                        button.coupon_code != null ? button.coupon_code.ToString() :
                                        button.flow_id != null ? button.flow_id.ToString() :
                                        null;

                                    // If BSP marks dynamic examples like Meta, respect them; otherwise be lenient
                                    buttons.Add(new ButtonMetadataDto
                                    {
                                        Text = text,
                                        Type = btnType,
                                        SubType = subType,
                                        Index = index,
                                        ParameterValue = paramValue ?? ""
                                    });
                                }
                                catch { /* ignore per-button parsing issues */ }
                            }
                        }
                    }
                }

                int placeholderCount = Regex.Matches(body ?? "", "{{(.*?)}}").Count;

                list.Add(new TemplateMetadataDto
                {
                    Name = name,
                    Language = language,
                    Body = body,
                    PlaceholderCount = placeholderCount,
                    HasImageHeader = hasImageHeader,
                    ButtonParams = buttons
                });
            }

            return list;
        }

        public async Task<List<TemplateForUIResponseDto>> FetchAllTemplatesAsync()
        {
            var result = new List<TemplateForUIResponseDto>();

            var user = _httpContextAccessor.HttpContext.User;
            var businessId = user.GetBusinessId();
            _logger.LogInformation("🔎 Fetching templates for BusinessId {BusinessId}", businessId);

            var setting = await _dbContext.WhatsAppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive && s.BusinessId == businessId);

            if (setting == null)
            {
                _logger.LogWarning("⚠️ No active WhatsApp setting for BusinessId {BusinessId}", businessId);
                return result;
            }

            try
            {
                // Canonical provider
                var provider = (setting.Provider ?? string.Empty)
                    .Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();

                // Normalize base URL once
                var baseRaw = (setting.ApiUrl ?? string.Empty).Trim().TrimEnd('/');

                if (provider == "META_CLOUD")
                {
                    // Meta requires ApiKey + WABA
                    if (string.IsNullOrWhiteSpace(setting.ApiKey) || string.IsNullOrWhiteSpace(setting.WabaId))
                    {
                        _logger.LogWarning("⚠️ Missing ApiKey or WabaId for Meta Cloud (Biz {BusinessId})", businessId);
                        return result;
                    }

                    var baseUrl = string.IsNullOrWhiteSpace(baseRaw) ? "https://graph.facebook.com/v22.0" : baseRaw;
                    var nextUrl = $"{baseUrl}/{setting.WabaId!.Trim()}/message_templates?limit=100";

                    while (!string.IsNullOrWhiteSpace(nextUrl))
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);

                        using var res = await _httpClient.SendAsync(req);
                        var json = await res.Content.ReadAsStringAsync();

                        _logger.LogInformation("📦 Meta Template API (Biz {BusinessId}) payload:\n{Json}", businessId, json);

                        if (!res.IsSuccessStatusCode)
                        {
                            _logger.LogError("❌ Meta template fetch failed (Biz {BusinessId}): {Json}", businessId, json);
                            break;
                        }

                        result.AddRange(ParseMetaTemplates(json));
                        nextUrl = JsonConvert.DeserializeObject<dynamic>(json)?.paging?.next?.ToString();
                    }
                }
                else if (provider == "PINNACLE")
                {
                    // Pinnacle requires ApiKey and pathId (WABA or default PhoneNumberId)
                    if (string.IsNullOrWhiteSpace(setting.ApiKey))
                    {
                        _logger.LogWarning("⚠️ Missing ApiKey for Pinnacle (Biz {BusinessId})", businessId);
                        return result;
                    }

                    // Prefer WABA; otherwise pull DEFAULT phoneNumberId from WhatsAppPhoneNumbers
                    string? pathId = !string.IsNullOrWhiteSpace(setting.WabaId)
                        ? setting.WabaId!.Trim()
                        : await _dbContext.WhatsAppPhoneNumbers
                            .AsNoTracking()
                            .Where(p => p.BusinessId == businessId
                                        && p.IsActive
                                        && p.Provider.ToUpper() == "PINNACLE")
                            .OrderByDescending(p => p.IsDefault)
                            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                            .Select(p => p.PhoneNumberId)
                            .FirstOrDefaultAsync();

                    if (string.IsNullOrWhiteSpace(pathId))
                    {
                        _logger.LogWarning("⚠️ Missing WabaId or default PhoneNumberId for Pinnacle (Biz {BusinessId})", businessId);
                        return result;
                    }

                    var baseUrl = string.IsNullOrWhiteSpace(baseRaw) ? "https://partnersv1.pinbot.ai" : baseRaw;
                    if (!baseUrl.EndsWith("/v3", StringComparison.OrdinalIgnoreCase)) baseUrl += "/v3";

                    var nextUrl = $"{baseUrl}/{pathId}/message_templates?limit=100";

                    while (!string.IsNullOrWhiteSpace(nextUrl))
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                        // Common header names Pinnacle accepts
                        req.Headers.TryAddWithoutValidation("apikey", setting.ApiKey);
                        req.Headers.TryAddWithoutValidation("x-api-key", setting.ApiKey);

                        using var res = await _httpClient.SendAsync(req);
                        var json = await res.Content.ReadAsStringAsync();

                        _logger.LogInformation("📦 Pinnacle Template API (Biz {BusinessId}) payload:\n{Json}", businessId, json);

                        if (!res.IsSuccessStatusCode)
                        {
                            _logger.LogError("❌ Pinnacle template fetch failed (Biz {BusinessId}): {Json}", businessId, json);
                            break;
                        }

                        // Your existing mapper handles Pinnacle/Meta-like shapes
                        result.AddRange(ParsePinnacleTemplates(json));

                        // If their API paginates like Meta, follow it; otherwise we’re done
                        nextUrl = JsonConvert.DeserializeObject<dynamic>(json)?.paging?.next?.ToString();
                        if (nextUrl == null) break;
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Unknown provider '{Provider}' for Biz {BusinessId}", provider, businessId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception while fetching templates for BusinessId {BusinessId}", businessId);
            }

            return result;
        }

        private List<TemplateForUIResponseDto> ParseMetaTemplates(string json)
        {
            var list = new List<TemplateForUIResponseDto>();
            dynamic parsed = JsonConvert.DeserializeObject<dynamic>(json);

            foreach (var tpl in parsed.data)
            {
                string status = (tpl.status?.ToString() ?? "").ToUpperInvariant();
                if (status != "APPROVED" && status != "ACTIVE") continue;

                list.Add(BuildTemplateDtoFromComponents(tpl));
            }

            return list;
        }

        private List<TemplateForUIResponseDto> ParsePinnacleTemplates(string json)
        {
            var list = new List<TemplateForUIResponseDto>();
            dynamic parsed = JsonConvert.DeserializeObject<dynamic>(json);

            if (parsed?.data == null) return list;

            foreach (var tpl in parsed.data)
            {
                // Pinnacle may not use status like Meta, adjust filter if needed
                list.Add(BuildTemplateDtoFromComponents(tpl));
            }

            return list;
        }

        private TemplateForUIResponseDto BuildTemplateDtoFromComponents(dynamic tpl)
        {
            string name = tpl.name;
            string language = tpl.language ?? "en_US";
            string body = "";
            bool hasImageHeader = false;
            var buttons = new List<ButtonMetadataDto>();

            foreach (var component in tpl.components)
            {
                string type = component.type?.ToString()?.ToUpperInvariant();

                if (type == "BODY")
                    body = component.text?.ToString() ?? "";

                if (type == "HEADER" && (component.format?.ToString()?.ToUpperInvariant() == "IMAGE"))
                    hasImageHeader = true;

                if (type == "BUTTONS")
                {
                    foreach (var button in component.buttons)
                    {
                        string btnType = button.type?.ToString()?.ToUpperInvariant() ?? "";
                        string text = button.text?.ToString() ?? "";
                        int index = buttons.Count;

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

                        string? paramValue = button.url?.ToString() ?? button.phone_number?.ToString();

                        if (subType == "unknown") continue;

                        buttons.Add(new ButtonMetadataDto
                        {
                            Text = text,
                            Type = btnType,
                            SubType = subType,
                            Index = index,
                            ParameterValue = paramValue ?? ""
                        });
                    }
                }
            }

            int placeholderCount = Regex.Matches(body ?? "", "{{(.*?)}}").Count;

            return new TemplateForUIResponseDto
            {
                Name = name,
                Language = language,
                Body = body,
                ParametersCount = placeholderCount,
                HasImageHeader = hasImageHeader,
                ButtonParams = buttons
            };
        }

        //public async Task<TemplateMetadataDto?> GetTemplateByNameAsync(Guid businessId, string templateName, bool includeButtons)
        //{
        //    var setting = await _dbContext.WhatsAppSettings
        //        .FirstOrDefaultAsync(x => x.IsActive && x.BusinessId == businessId);

        //    if (setting == null)
        //    {
        //        _logger.LogWarning("❌ WhatsApp settings not found for business: {BusinessId}", businessId);
        //        return null;
        //    }

        //    var provider = (setting.Provider ?? "meta_cloud").Trim().ToLowerInvariant();
        //    var wabaId = setting.WabaId?.Trim();
        //    if (string.IsNullOrWhiteSpace(wabaId))
        //    {
        //        _logger.LogWarning("❌ Missing WABA ID for business: {BusinessId}", businessId);
        //        return null;
        //    }

        //    // Build URL + request with per-request headers
        //    string url;
        //    using var req = new HttpRequestMessage(HttpMethod.Get, "");

        //    if (provider == "pinnacle")
        //    {
        //        // Pinnacle: require ApiKey; use WabaId for template listing
        //        if (string.IsNullOrWhiteSpace(setting.ApiKey))
        //        {
        //            _logger.LogWarning("❌ ApiKey missing for Pinnacle provider (BusinessId {BusinessId})", businessId);
        //            return null;
        //        }

        //        var baseUrl = string.IsNullOrWhiteSpace(setting.ApiUrl)
        //            ? "https://partnersv1.pinbot.ai/v3"
        //            : setting.ApiUrl.TrimEnd('/');

        //        url = $"{baseUrl}/{wabaId}/message_templates?limit=200";
        //        // add header variants
        //        req.Headers.TryAddWithoutValidation("apikey", setting.ApiKey);
        //        req.Headers.TryAddWithoutValidation("x-api-key", setting.ApiKey);
        //        // safety: also append as query (some edges require it)
        //        url = url.Contains("apikey=") ? url : $"{url}&apikey={Uri.EscapeDataString(setting.ApiKey)}";
        //    }
        //    else // meta_cloud
        //    {
        //        // Meta Cloud: require ApiKey; use WabaId for template listing
        //        if (string.IsNullOrWhiteSpace(setting.ApiKey))
        //        {
        //            _logger.LogWarning("❌ ApiKey missing for Meta provider (BusinessId {BusinessId})", businessId);
        //            return null;
        //        }

        //        var baseUrl = string.IsNullOrWhiteSpace(setting.ApiUrl)
        //            ? "https://graph.facebook.com/v22.0"
        //            : setting.ApiUrl.TrimEnd('/');

        //        url = $"{baseUrl}/{wabaId}/message_templates?limit=200";
        //        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);
        //    }

        //    req.RequestUri = new Uri(url);
        //    var response = await _httpClient.SendAsync(req);
        //    var json = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        _logger.LogError("❌ Failed to fetch templates (provider={Provider}) for BusinessId {BusinessId}: HTTP {Status} Body: {Body}",
        //            provider, businessId, (int)response.StatusCode, json);
        //        return null;
        //    }

        //    try
        //    {
        //        dynamic parsed = JsonConvert.DeserializeObject<dynamic>(json);
        //        var data = parsed?.data;
        //        if (data == null)
        //        {
        //            _logger.LogWarning("⚠️ No 'data' array in template response (provider={Provider})", provider);
        //            return null;
        //        }

        //        foreach (var tpl in data)
        //        {
        //            string name = tpl.name;
        //            if (!name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
        //                continue;

        //            string language = tpl.language != null ? (string)tpl.language : "en_US";
        //            string body = "";
        //            var buttons = new List<ButtonMetadataDto>();
        //            bool hasImageHeader = false;

        //            // components loop
        //            foreach (var component in tpl.components)
        //            {
        //                string type = component.type?.ToString()?.ToUpperInvariant();

        //                if (type == "BODY")
        //                {
        //                    try { body = component.text?.ToString() ?? ""; }
        //                    catch { body = ""; }
        //                }

        //                if (type == "HEADER")
        //                {
        //                    string format = component.format?.ToString()?.ToUpperInvariant();
        //                    if (format == "IMAGE") hasImageHeader = true;
        //                }

        //                if (includeButtons && type == "BUTTONS")
        //                {
        //                    foreach (var button in component.buttons)
        //                    {
        //                        try
        //                        {
        //                            string btnType = button.type?.ToString()?.ToUpperInvariant() ?? "";
        //                            string text = button.text?.ToString() ?? "";
        //                            int index = buttons.Count;

        //                            // normalize sub-type for our app
        //                            string subType = btnType switch
        //                            {
        //                                "URL" => "url",
        //                                "PHONE_NUMBER" => "voice_call",
        //                                "QUICK_REPLY" => "quick_reply",
        //                                "COPY_CODE" => "copy_code",
        //                                "CATALOG" => "catalog",
        //                                "FLOW" => "flow",
        //                                "REMINDER" => "reminder",
        //                                "ORDER_DETAILS" => "order_details",
        //                                _ => "unknown"
        //                            };

        //                            // Known dynamic param extraction
        //                            string? paramValue = null;
        //                            if (button.url != null)
        //                                paramValue = button.url.ToString();
        //                            else if (button.phone_number != null)
        //                                paramValue = button.phone_number.ToString();
        //                            else if (button.coupon_code != null)
        //                                paramValue = button.coupon_code.ToString();
        //                            else if (button.flow_id != null)
        //                                paramValue = button.flow_id.ToString();

        //                            // Skip truly invalid
        //                            if (subType == "unknown" ||
        //                                (paramValue == null && new[] { "url", "flow", "copy_code" }.Contains(subType)))
        //                            {
        //                                _logger.LogWarning("⚠️ Skipping button '{Text}' due to unknown type or missing required param.", text);
        //                                continue;
        //                            }

        //                            buttons.Add(new ButtonMetadataDto
        //                            {
        //                                Text = text,
        //                                Type = btnType,
        //                                SubType = subType,
        //                                Index = index,
        //                                ParameterValue = paramValue ?? "" // empty for static buttons
        //                            });
        //                        }
        //                        catch (Exception exBtn)
        //                        {
        //                            _logger.LogWarning(exBtn, "⚠️ Failed to parse button in template {TemplateName}", name);
        //                        }
        //                    }
        //                }
        //            }

        //            // Count {{n}} placeholders in body
        //            int paramCount = Regex.Matches(body ?? "", "{{\\s*\\d+\\s*}}").Count;

        //            return new TemplateMetadataDto
        //            {
        //                Name = name,
        //                Language = language,
        //                Body = body,
        //                PlaceholderCount = paramCount,
        //                HasImageHeader = hasImageHeader,
        //                ButtonParams = includeButtons ? buttons : new List<ButtonMetadataDto>()
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "❌ Exception while parsing template response");
        //    }

        //    return null;
        //}
        //public async Task<TemplateMetadataDto?> GetTemplateByNameAsync(Guid businessId, string templateName, bool includeButtons)
        //{
        //    var setting = await _dbContext.WhatsAppSettings
        //        .FirstOrDefaultAsync(x => x.IsActive && x.BusinessId == businessId);

        //    if (setting == null)
        //    {
        //        _logger.LogWarning("❌ WhatsApp settings not found for business: {BusinessId}", businessId);
        //        return null;
        //    }

        //    var provider = (setting.Provider ?? "meta_cloud").Trim().ToLowerInvariant();
        //    var wabaId = setting.WabaId?.Trim();
        //    if (string.IsNullOrWhiteSpace(wabaId))
        //    {
        //        _logger.LogWarning("❌ Missing WABA ID for business: {BusinessId}", businessId);
        //        return null;
        //    }

        //    // Build URL + request with per-request headers
        //    string url;
        //    using var req = new HttpRequestMessage(HttpMethod.Get, "");

        //    if (provider == "pinnacle")
        //    {
        //        if (string.IsNullOrWhiteSpace(setting.ApiKey))
        //        {
        //            _logger.LogWarning("❌ ApiKey missing for Pinnacle provider (BusinessId {BusinessId})", businessId);
        //            return null;
        //        }

        //        var baseUrl = string.IsNullOrWhiteSpace(setting.ApiUrl)
        //            ? "https://partnersv1.pinbot.ai/v3"
        //            : setting.ApiUrl.TrimEnd('/');

        //        url = $"{baseUrl}/{wabaId}/message_templates?limit=200";
        //        req.Headers.TryAddWithoutValidation("apikey", setting.ApiKey);
        //        req.Headers.TryAddWithoutValidation("x-api-key", setting.ApiKey);
        //        url = url.Contains("apikey=") ? url : $"{url}&apikey={Uri.EscapeDataString(setting.ApiKey)}";
        //    }
        //    else // meta_cloud
        //    {
        //        if (string.IsNullOrWhiteSpace(setting.ApiKey))
        //        {
        //            _logger.LogWarning("❌ ApiKey missing for Meta provider (BusinessId {BusinessId})", businessId);
        //            return null;
        //        }

        //        var baseUrl = string.IsNullOrWhiteSpace(setting.ApiUrl)
        //            ? "https://graph.facebook.com/v22.0"
        //            : setting.ApiUrl.TrimEnd('/');

        //        url = $"{baseUrl}/{wabaId}/message_templates?limit=200";
        //        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);
        //    }

        //    req.RequestUri = new Uri(url);
        //    var response = await _httpClient.SendAsync(req);
        //    var json = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        _logger.LogError("❌ Failed to fetch templates (provider={Provider}) for BusinessId {BusinessId}: HTTP {Status} Body: {Body}",
        //            provider, businessId, (int)response.StatusCode, json);
        //        return null;
        //    }

        //    try
        //    {
        //        dynamic parsed = JsonConvert.DeserializeObject<dynamic>(json);
        //        var data = parsed?.data;
        //        if (data == null)
        //        {
        //            _logger.LogWarning("⚠️ No 'data' array in template response (provider={Provider})", provider);
        //            return null;
        //        }

        //        foreach (var tpl in data)
        //        {
        //            string name = tpl.name;
        //            if (!name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
        //                continue;

        //            string language = tpl.language != null ? (string)tpl.language : "en_US";

        //            // ---- CHANGES START ----
        //            string body = "";
        //            string headerText = "";                    // collect TEXT header
        //            bool hasImageHeader = false;
        //            var buttons = new List<ButtonMetadataDto>();

        //            foreach (var component in tpl.components)
        //            {
        //                string type = component.type?.ToString()?.ToUpperInvariant();

        //                if (type == "HEADER")
        //                {
        //                    string format = component.format?.ToString()?.ToUpperInvariant();
        //                    if (format == "IMAGE")
        //                        hasImageHeader = true;

        //                    // capture header text (can include {{n}})
        //                    if (format == "TEXT")
        //                    {
        //                        try { headerText = component.text?.ToString() ?? ""; }
        //                        catch { headerText = ""; }
        //                    }
        //                }

        //                if (type == "BODY")
        //                {
        //                    try { body = component.text?.ToString() ?? ""; }
        //                    catch { body = ""; }
        //                }

        //                if (includeButtons && type == "BUTTONS")
        //                {
        //                    foreach (var button in component.buttons)
        //                    {
        //                        try
        //                        {
        //                            string btnType = button.type?.ToString()?.ToUpperInvariant() ?? "";
        //                            string text = button.text?.ToString() ?? "";
        //                            int index = buttons.Count;

        //                            string subType = btnType switch
        //                            {
        //                                "URL" => "url",
        //                                "PHONE_NUMBER" => "voice_call",
        //                                "QUICK_REPLY" => "quick_reply",
        //                                "COPY_CODE" => "copy_code",
        //                                "CATALOG" => "catalog",
        //                                "FLOW" => "flow",
        //                                "REMINDER" => "reminder",
        //                                "ORDER_DETAILS" => "order_details",
        //                                _ => "unknown"
        //                            };

        //                            string? paramValue = null;
        //                            if (button.url != null) paramValue = button.url.ToString();
        //                            else if (button.phone_number != null) paramValue = button.phone_number.ToString();
        //                            else if (button.coupon_code != null) paramValue = button.coupon_code.ToString();
        //                            else if (button.flow_id != null) paramValue = button.flow_id.ToString();

        //                            if (subType == "unknown" ||
        //                                (paramValue == null && new[] { "url", "flow", "copy_code" }.Contains(subType)))
        //                            {
        //                                _logger.LogWarning("⚠️ Skipping button '{Text}' due to unknown type or missing required param.", text);
        //                                continue;
        //                            }

        //                            buttons.Add(new ButtonMetadataDto
        //                            {
        //                                Text = text,
        //                                Type = btnType,
        //                                SubType = subType,
        //                                Index = index,
        //                                ParameterValue = paramValue ?? ""
        //                            });
        //                        }
        //                        catch (Exception exBtn)
        //                        {
        //                            _logger.LogWarning(exBtn, "⚠️ Failed to parse button in template {TemplateName}", name);
        //                        }
        //                    }
        //                }
        //            }

        //            // Combine header + body so your DB stores what users actually see
        //            var combined = string.IsNullOrWhiteSpace(headerText)
        //                ? body
        //                : (string.IsNullOrWhiteSpace(body) ? headerText : $"{headerText}\n{body}");

        //            // Count {{n}} across header+body
        //            int paramCount = Regex.Matches(combined ?? "", "{{\\s*\\d+\\s*}}").Count;
        //            // ---- CHANGES END ----

        //            return new TemplateMetadataDto
        //            {
        //                Name = name,
        //                Language = language,
        //                Body = combined,                         // return combined text
        //                PlaceholderCount = paramCount,
        //                HasImageHeader = hasImageHeader,
        //                ButtonParams = includeButtons ? buttons : new List<ButtonMetadataDto>()
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "❌ Exception while parsing template response");
        //    }

        //    return null;
        //}
        // Features/CampaignModule/Services/TemplateFetcherService.cs



        public async Task<TemplateMetadataDto?> GetTemplateByNameAsync(
            Guid businessId,
            string templateName,
            bool includeButtons)
        {
            var row = await _dbContext.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == templateName)
                .OrderByDescending(t => t.UpdatedAt) // or LastSyncedAt if you prefer
                .FirstOrDefaultAsync();

            if (row == null) return null;

            string headerKind = "none";
            string combinedBody = row.Body ?? string.Empty;
            int placeholderCount = 0;

            if (!string.IsNullOrWhiteSpace(row.RawJson))
            {
                // Use canonical computation from provider JSON (handles header/body/buttons & NAMED/POSITIONAL)
                var s = TemplateJsonHelper.Summarize(row.RawJson, row.Body);
                headerKind = s.HeaderKind ?? "none";
                combinedBody = s.CombinedBody ?? string.Empty;
                placeholderCount = s.PlaceholderCount;
            }
            else
            {
                // No RawJson: fall back to counting tokens in body and buttons JSON
                placeholderCount =
                    CountTokensFlexible(combinedBody) +
                    CountButtonTokensFromButtonsJson(row.UrlButtons);
            }

            var buttons = includeButtons ? ParseButtons(row.UrlButtons) : new List<ButtonMetadataDto>();

            return new TemplateMetadataDto
            {
                Name = row.Name,
                Language = row.LanguageCode ?? "en_US",
                Body = combinedBody,
                PlaceholderCount = placeholderCount,
                HeaderKind = headerKind,
                HasImageHeader = string.Equals(headerKind, "image", StringComparison.OrdinalIgnoreCase), // back-compat
                ButtonParams = buttons
            };
        }

        // -------- helpers (keep inside the same class) --------

        private static readonly Regex PositionalToken =
            new(@"\{\{\s*\d+\s*\}\}", RegexOptions.Compiled); // {{1}}, {{ 2 }}, etc.

        private static readonly Regex NamedToken =
            new(@"\{\{\s*\}\}", RegexOptions.Compiled);        // {{}} (NAMED format slot)

        private static int CountTokensFlexible(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return PositionalToken.Matches(text).Count + NamedToken.Matches(text).Count;
        }

        private static int CountButtonTokensFromButtonsJson(string? buttonsJson)
        {
            if (string.IsNullOrWhiteSpace(buttonsJson)) return 0;

            try
            {
                var arr = JArray.Parse(buttonsJson);
                int total = 0;
                foreach (var b in arr)
                {
                    // Your ButtonsJson comes from ButtonMetadataDto serialization and includes "ParameterValue"
                    var val = b?["ParameterValue"]?.ToString();
                    total += CountTokensFlexible(val);
                }
                return total;
            }
            catch
            {
                // If ButtonsJson is malformed, don't block reads—just report 0 extras.
                return 0;
            }
        }




        private static string DetectHeaderKind(string? rawJson)
        {
            try
            {
                var comps = (JToken.Parse(rawJson ?? "{}")["components"] as JArray) ?? new JArray();
                var header = comps.FirstOrDefault(c => string.Equals((string?)c?["type"], "HEADER", StringComparison.OrdinalIgnoreCase));
                var fmt = ((string?)header?["format"] ?? "").ToLowerInvariant(); // "", "text", "image", "video", "document", "location"
                return string.IsNullOrWhiteSpace(fmt) ? "none" : fmt;
            }
            catch { return "none"; }
        }

        private static string CombineTextHeaderAndBody(string? rawJson, string? fallbackBody)
        {
            var headerText = ExtractTextHeader(rawJson);
            var bodyText = ExtractBodyText(rawJson);
            if (string.IsNullOrWhiteSpace(bodyText)) bodyText = fallbackBody ?? "";
            if (!string.IsNullOrWhiteSpace(headerText))
                return headerText.TrimEnd() + "\n" + (bodyText ?? "");
            return bodyText ?? "";
        }

        private static string ExtractTextHeader(string? rawJson)
        {
            try
            {
                var comps = (JToken.Parse(rawJson ?? "{}")["components"] as JArray) ?? new JArray();
                var header = comps.FirstOrDefault(c => string.Equals((string?)c?["type"], "HEADER", StringComparison.OrdinalIgnoreCase));
                if (header == null) return "";
                if (!string.Equals((string?)header["format"], "TEXT", StringComparison.OrdinalIgnoreCase)) return "";
                return header["text"]?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private static string ExtractBodyText(string? rawJson)
        {
            try
            {
                var comps = (JToken.Parse(rawJson ?? "{}")["components"] as JArray) ?? new JArray();
                var body = comps.FirstOrDefault(c => string.Equals((string?)c?["type"], "BODY", StringComparison.OrdinalIgnoreCase));
                return body?["text"]?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private static List<ButtonMetadataDto> ParseButtons(string? buttonsJson)
        {
            try
            {
                var arr = JArray.Parse(string.IsNullOrWhiteSpace(buttonsJson) ? "[]" : buttonsJson);
                return arr.Select(j => new ButtonMetadataDto
                {
                    Text = (string?)j["Text"] ?? "",
                    Type = (string?)j["Type"] ?? "URL",
                    SubType = ((string?)j["SubType"] ?? "url").ToLowerInvariant(),
                    Index = (int?)j["Index"] ?? 0,
                    ParameterValue = (string?)j["ParameterValue"] ?? ""
                }).ToList();
            }
            catch { return new List<ButtonMetadataDto>(); }
        }


        private static TemplateMetadataDto? ExtractTemplateFromListJson(string json, string templateName, bool includeButtons)
        {
            var root = JObject.Parse(json);
            var data = root["data"] as JArray;
            if (data == null) return null;

            foreach (var tplToken in data.OfType<JObject>())
            {
                var name = tplToken.Value<string>("name") ?? "";
                if (!name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var language = tplToken.Value<string>("language") ?? "en_US";
                var components = tplToken["components"] as JArray;

                string body = "";
                bool hasImageHeader = false;
                var buttons = new List<ButtonMetadataDto>();

                if (components != null)
                {
                    foreach (var comp in components.OfType<JObject>())
                    {
                        var type = (comp.Value<string>("type") ?? "").ToUpperInvariant();

                        if (type == "BODY")
                        {
                            body = comp.Value<string>("text") ?? body;
                        }
                        else if (type == "HEADER")
                        {
                            var fmt = (comp.Value<string>("format") ?? "").ToUpperInvariant();
                            if (fmt == "IMAGE") hasImageHeader = true;
                        }
                        else if (includeButtons && type == "BUTTONS")
                        {
                            var btns = comp["buttons"] as JArray;
                            if (btns == null) continue;

                            var idx = 0;
                            foreach (var b in btns.OfType<JObject>())
                            {
                                var btnTypeRaw = (b.Value<string>("type") ?? "").ToUpperInvariant();
                                var text = b.Value<string>("text") ?? "";

                                var subType = btnTypeRaw switch
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

                                string? paramValue =
                                    b.Value<string>("url") ??
                                    b.Value<string>("phone_number") ??
                                    b.Value<string>("coupon_code") ??
                                    b.Value<string>("flow_id");

                                // Skip unknown or missing required dynamic values
                                if (subType == "unknown") continue;
                                if ((subType is "url" or "flow" or "copy_code") && string.IsNullOrWhiteSpace(paramValue))
                                    continue;

                                buttons.Add(new ButtonMetadataDto
                                {
                                    Text = text,
                                    Type = btnTypeRaw,
                                    SubType = subType,
                                    Index = idx++,
                                    ParameterValue = paramValue ?? ""
                                });
                            }
                        }
                    }
                }

                var paramCount = Regex.Matches(body ?? "", "{{(.*?)}}").Count;

                return new TemplateMetadataDto
                {
                    Name = name,
                    Language = language,
                    Body = body ?? "",
                    PlaceholderCount = paramCount,
                    HasImageHeader = hasImageHeader,
                    ButtonParams = includeButtons ? buttons : new List<ButtonMetadataDto>()
                };
            }

            return null;
        }

        // --- NEW: DB-backed meta projection methods ---
        // List all template meta for a business (optionally filter by provider)
        public async Task<IReadOnlyList<TemplateMetaDto>> GetTemplatesMetaAsync(Guid businessId, string? provider = null)
        {
            var q = _dbContext.WhatsAppTemplates
                              .AsNoTracking()
                              .Where(t => t.BusinessId == businessId && t.IsActive);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(t => t.Provider == provider);

            // Pull only what we need, then map (keeps allocations low on hot paths)
            var rows = await q.ToListAsync();
            var result = new List<TemplateMetaDto>(rows.Count);
            foreach (var row in rows)
                result.Add(MapRowToMeta(row));
            return result;
        }

        // Get a single template meta by name (+ optional language/provider)
        public async Task<TemplateMetaDto?> GetTemplateMetaAsync(
    Guid businessId,
    string templateName,
    string? language = null,
    string? provider = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            var lang = language?.Trim();
            var prov = string.IsNullOrWhiteSpace(provider) ? null : provider!.Trim().ToUpperInvariant();

            // Base: active rows for this business
            var q = _dbContext.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.IsActive);

            // First try: treat templateName as provider TemplateId (exact match)
            var byIdQ = q.Where(t => t.TemplateId == templateName);
            if (prov != null) byIdQ = byIdQ.Where(t => t.Provider == prov);
            if (lang != null) byIdQ = byIdQ.Where(t => t.LanguageCode == lang);

            var pick = await byIdQ
                .OrderByDescending(t => t.UpdatedAt)
                .ThenByDescending(t => t.LastSyncedAt)
                .ThenByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (pick == null)
            {
                // Second try: match by logical Name (+provider/lang if provided)
                var byNameQ = q.Where(t => t.Name == templateName);
                if (prov != null) byNameQ = byNameQ.Where(t => t.Provider == prov);

                // Prefer an exact language match if provided; then fall back to newest any-language
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    pick = await byNameQ
                        .OrderByDescending(t => t.LanguageCode == lang) // exact lang first
                        .ThenByDescending(t => t.UpdatedAt)
                        .ThenByDescending(t => t.LastSyncedAt)
                        .ThenByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    pick = await byNameQ
                        .OrderByDescending(t => t.UpdatedAt)
                        .ThenByDescending(t => t.LastSyncedAt)
                        .ThenByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();
                }
            }

            return pick == null ? null : MapRowToMeta(pick);
        }


        // Map persisted catalog row → TemplateMetaDto
        //private DTO.TemplateMetaDto MapRowToMeta(dynamic row)
        //{
        //    var meta = new DTO.TemplateMetaDto
        //    {
        //        Provider = row.Provider ?? "",
        //        TemplateId = row.ExternalId ?? row.TemplateId ?? "",
        //        TemplateName = row.Name ?? "",
        //        Language = row.Language ?? "",
        //        HasHeaderMedia = row.HasImageHeader ?? false,
        //        HeaderType = (row.HasImageHeader ?? false) ? "IMAGE" : ""
        //    };

        //    int count = 0;
        //    try { count = Convert.ToInt32(row.PlaceholderCount ?? 0); } catch { count = 0; }

        //    meta.BodyPlaceholders = Enumerable.Range(1, Math.Max(0, count))
        //                                      .Select(i => new DTO.PlaceholderSlot { Index = i })
        //                                      .ToList();

        //    // ButtonsJson → TemplateButtonMeta[]
        //    meta.Buttons = new List<DTO.TemplateButtonMeta>();
        //    try
        //    {
        //        string buttonsJson = row.ButtonsJson ?? "";
        //        if (!string.IsNullOrWhiteSpace(buttonsJson))
        //        {
        //            using var doc = JsonDocument.Parse(buttonsJson);
        //            if (doc.RootElement.ValueKind == JsonValueKind.Array)
        //            {
        //                int i = 0;
        //                foreach (var el in doc.RootElement.EnumerateArray())
        //                {
        //                    var type = el.TryGetProperty("Type", out var p1) ? p1.GetString() ?? ""
        //                              : el.TryGetProperty("type", out var p1b) ? p1b.GetString() ?? "" : "";
        //                    var text = el.TryGetProperty("Text", out var p2) ? p2.GetString() ?? ""
        //                              : el.TryGetProperty("text", out var p2b) ? p2b.GetString() ?? "" : "";
        //                    var value = el.TryGetProperty("ParameterValue", out var p3) ? p3.GetString()
        //                              : el.TryGetProperty("value", out var p3b) ? p3b.GetString() : null;
        //                    var order = el.TryGetProperty("Index", out var p4) && p4.ValueKind == JsonValueKind.Number
        //                              ? p4.GetInt32()
        //                              : i;

        //                    meta.Buttons.Add(new DTO.TemplateButtonMeta
        //                    {
        //                        Type = type,
        //                        Text = text,
        //                        Value = value,
        //                        Order = order
        //                    });
        //                    i++;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Failed to parse ButtonsJson for template {TemplateName}", (string)(row.Name ?? "(unknown)"));
        //    }

        //    return meta;
        //}
        // add near the class top (once)
        // helpers (place near the class top)
        private static readonly Regex _phRx = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);

        private static int DistinctMaxPlaceholderIndex(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var m = _phRx.Matches(text);
            int max = 0;
            var seen = new HashSet<int>();
            foreach (Match x in m)
            {
                if (int.TryParse(x.Groups[1].Value, out var i) && seen.Add(i) && i > max)
                    max = i;
            }
            return max;
        }

        private static bool TryGetPropCI(JsonElement obj, string name, out JsonElement value)
        {
            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        // Minimal allocations; assumes DTO types live in xbytechat.api.WhatsAppSettings.DTOs
        private static TemplateMetaDto MapRowToMeta(WhatsAppTemplate row)
        {
            // Provider + IDs
            var dto = new TemplateMetaDto
            {
                Provider = row.Provider,                   // "META_CLOUD" | "PINNACLE"
                TemplateId = row.TemplateId ?? "",
                TemplateName = row.Name,
                Language = row.LanguageCode,               // <- final field
            };

            // Header/media
            var hk = (row.HeaderKind ?? "none").Trim().ToLowerInvariant();
            dto.HasHeaderMedia = row.RequiresMediaHeader || hk is "image" or "video" or "document";
            dto.HeaderType = hk switch
            {
                "image" => "IMAGE",
                "video" => "VIDEO",
                "document" => "DOCUMENT",
                _ => null
            };

            // BODY placeholders: we only store counts now, so synthesize 1..BodyVarCount
            // (TemplateMetaDto consumers only need index order for preview/validation)
            dto.BodyPlaceholders = new List<PlaceholderSlot>(row.BodyVarCount);
            for (int i = 1; i <= row.BodyVarCount; i++)
                dto.BodyPlaceholders.Add(new PlaceholderSlot { Index = i });

            // Buttons (up to 3): reconstruct from UrlButtons (+ quick-replies + phone)
            // UrlButtons json shape we persisted: [{ index, parameters:[...] }, ...]
            var buttons = new List<TemplateButtonMeta>(3);

            // URL buttons first (keep template order if present)
            if (!string.IsNullOrWhiteSpace(row.UrlButtons))
            {
                try
                {
                    var arr = Newtonsoft.Json.Linq.JArray.Parse(row.UrlButtons);
                    foreach (var j in arr)
                    {
                        if (buttons.Count >= 3) break;
                        var order = (int?)j?["index"] ?? buttons.Count; // fallback order
                        buttons.Add(new TemplateButtonMeta
                        {
                            Type = "URL",
                            Text = "",     // label not stored; UI doesn’t require it for preview send
                            Value = null,   // value may contain "{{1}}" at send-time
                            Order = order
                        });
                    }
                }
                catch { /* ignore malformed json */ }
            }

            // Phone button (at most one)
            if (buttons.Count < 3 && row.HasPhoneButton)
            {
                buttons.Add(new TemplateButtonMeta
                {
                    Type = "PHONE_NUMBER",
                    Text = "",
                    Value = null,
                    Order = buttons.Count
                });
            }

            // Quick replies (add as many as persisted, up to remaining slots)
            var toAdd = Math.Min(row.QuickReplyCount, Math.Max(0, 3 - buttons.Count));
            for (int i = 0; i < toAdd; i++)
            {
                buttons.Add(new TemplateButtonMeta
                {
                    Type = "QUICK_REPLY",
                    Text = "",
                    Value = null,
                    Order = buttons.Count
                });
            }

            dto.Buttons = buttons;
            return dto;
        }


    }
}

