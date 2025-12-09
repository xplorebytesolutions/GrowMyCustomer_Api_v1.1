using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using xbytechat.api.WhatsAppSettings.Abstractions;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.WhatsAppSettings.Providers
{
    public sealed class MetaTemplateCatalogProvider : IMetaTemplateCatalogProvider
    {
        private readonly HttpClient _http;
        private readonly ILogger<MetaTemplateCatalogProvider> _log;

        public MetaTemplateCatalogProvider(HttpClient http, ILogger<MetaTemplateCatalogProvider> log)
        {
            _http = http;
            _log = log;
        }

        public async Task<IReadOnlyList<TemplateCatalogItem>> ListMetaAsync(
            WhatsAppSettingEntity s, CancellationToken ct = default)
        {
            var items = new List<TemplateCatalogItem>();
            if (string.IsNullOrWhiteSpace(s.ApiKey) || string.IsNullOrWhiteSpace(s.WabaId))
                return items;

            var baseUrl = s.ApiUrl?.TrimEnd('/') ?? "https://graph.facebook.com/v22.0";
            var next = $"{baseUrl}/{s.WabaId}/message_templates?limit=100";

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            while (!string.IsNullOrWhiteSpace(next))
            {
                ct.ThrowIfCancellationRequested();

                using var req = new HttpRequestMessage(HttpMethod.Get, next);
                var res = await _http.SendAsync(req, ct);
                var json = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode) break;

                dynamic parsed = JsonConvert.DeserializeObject(json);
                foreach (var tpl in parsed.data)
                {
                    try
                    {
                        // status filter
                        string status = (tpl.status?.ToString() ?? "").ToUpperInvariant();
                        if (status != "APPROVED" && status != "ACTIVE") continue;

                        string name = tpl.name?.ToString() ?? "";
                        string language = tpl.language?.ToString() ?? "en_US";
                        string category = tpl.category?.ToString() ?? "";
                        string externalId = tpl.id?.ToString() ?? "";

                        string body = "";
                        bool hasImageHeader = false;
                        var buttons = new List<ButtonMetadataDto>();

                        foreach (var comp in tpl.components)
                        {
                            string type = comp.type?.ToString()?.ToUpperInvariant() ?? "";

                            if (type == "BODY")
                            {
                                body = comp.text?.ToString() ?? "";
                            }
                            else if (type == "HEADER")
                            {
                                if (comp.format?.ToString()?.ToUpperInvariant() == "IMAGE")
                                    hasImageHeader = true;
                            }
                            else if (type == "BUTTONS")
                            {
                                if (comp.buttons == null) continue;

                                foreach (var b in comp.buttons)
                                {
                                    try
                                    {
                                        string btnTypeRaw = b.type?.ToString() ?? "";
                                        string btnType = btnTypeRaw.ToUpperInvariant();
                                        string text = b.text?.ToString() ?? "";

                                        // ---- robust index extraction (avoid RuntimeBinderException) ----
                                        int index;
                                        object? idxObj = null;
                                        try { idxObj = b.index; } catch { /* some payloads omit index */ }
                                        if (idxObj is int ii) index = ii;
                                        else if (idxObj is long ll && ll <= int.MaxValue) index = (int)ll;
                                        else if (idxObj != null && int.TryParse(idxObj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIdx))
                                            index = parsedIdx;
                                        else
                                            index = buttons.Count; // fallback to current count

                                        // normalize subtype & parameter value
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

                                        // try to capture a useful value for URL-like buttons
                                        string? param =
                                            b.url != null ? b.url.ToString() :
                                            b.phone_number != null ? b.phone_number.ToString() :
                                            b.coupon_code != null ? b.coupon_code.ToString() :
                                            b.flow_id != null ? b.flow_id.ToString() :
                                            null;

                                        // “example” can be array or scalar
                                        if (string.IsNullOrWhiteSpace(param))
                                        {
                                            try
                                            {
                                                var ex = b.example;
                                                if (ex != null)
                                                {
                                                    if (ex is string exStr) param = exStr;
                                                    else
                                                    {
                                                        // attempt common shapes: { "example": { "url": ["..."] } } or ["..."]
                                                        var exStr2 = ex.ToString();
                                                        if (!string.IsNullOrWhiteSpace(exStr2))
                                                            param = exStr2;
                                                    }
                                                }
                                            }
                                            catch { /* ignore */ }
                                        }

                                        // some subtypes require a value; skip if not available
                                        bool requiresParam = subType is "url" or "flow" or "copy_code" or "catalog" or "reminder";
                                        if (subType == "unknown" || (requiresParam && string.IsNullOrWhiteSpace(param)))
                                            continue;

                                        buttons.Add(new ButtonMetadataDto
                                        {
                                            Text = text,
                                            Type = btnType,
                                            SubType = subType,
                                            Index = index,
                                            ParameterValue = param ?? ""
                                        });
                                    }
                                    catch (Exception exBtn)
                                    {
                                        _log.LogWarning(exBtn, "[MetaTpl] Button parse failed for template {Name}", name);
                                    }
                                }
                            }
                        }

                        int placeholders = Regex.Matches(body ?? "", "{{(.*?)}}").Count;
                        var raw = JsonConvert.SerializeObject(tpl);

                        items.Add(new TemplateCatalogItem(
                            Name: name,
                            Language: language,
                            Body: body,
                            PlaceholderCount: placeholders,
                            HasImageHeader: hasImageHeader,
                            Buttons: buttons,
                            Status: status,
                            Category: category,
                            ExternalId: externalId,
                            RawJson: raw
                        ));
                    }
                    catch (Exception exItem)
                    {
                        _log.LogWarning(exItem, "[MetaTpl] Skipped bad template item");
                    }
                }

                // paging
                try { next = parsed?.paging?.next?.ToString(); }
                catch { next = null; }
            }

            return items;
        }

        public async Task<TemplateCatalogItem?> GetByNameMetaAsync(
            WhatsAppSettingEntity s, string templateName, CancellationToken ct = default)
        {
            var all = await ListMetaAsync(s, ct);
            return all.FirstOrDefault(t => t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }
    }
}

//using Newtonsoft.Json;
//using System.Net.Http.Headers;
//using System.Text.RegularExpressions;
//using xbytechat.api.WhatsAppSettings.Abstractions;
//using xbytechat.api.WhatsAppSettings.DTOs;
//using xbytechat_api.WhatsAppSettings.Models;

//namespace xbytechat.api.WhatsAppSettings.Providers
//{
//    public sealed class MetaTemplateCatalogProvider : ITemplateCatalogProvider
//    {
//        private readonly HttpClient _http;
//        private readonly ILogger<MetaTemplateCatalogProvider> _log;

//        public MetaTemplateCatalogProvider(HttpClient http, ILogger<MetaTemplateCatalogProvider> log)
//        { _http = http; _log = log; }

//        public async Task<IReadOnlyList<TemplateCatalogItem>> ListAsync(WhatsAppSettingEntity s, CancellationToken ct = default)
//        {
//            var items = new List<TemplateCatalogItem>();
//            if (string.IsNullOrWhiteSpace(s.ApiKey) || string.IsNullOrWhiteSpace(s.WabaId))
//                return items;

//            var baseUrl = s.ApiUrl?.TrimEnd('/') ?? "https://graph.facebook.com/v22.0";
//            var next = $"{baseUrl}/{s.WabaId}/message_templates?limit=100";

//            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

//            while (!string.IsNullOrWhiteSpace(next))
//            {
//                var res = await _http.GetAsync(next, ct);
//                var json = await res.Content.ReadAsStringAsync(ct);
//                if (!res.IsSuccessStatusCode) break;

//                dynamic parsed = JsonConvert.DeserializeObject(json);

//                foreach (var tpl in parsed.data)
//                {
//                    // Filter APPROVED/ACTIVE
//                    string status = (tpl.status?.ToString() ?? "").ToUpperInvariant();
//                    if (status != "APPROVED" && status != "ACTIVE") continue;

//                    string name = tpl.name;
//                    string language = tpl.language ?? "en_US";
//                    string body = "";
//                    bool hasImageHeader = false;
//                    var buttons = new List<ButtonMetadataDto>();

//                    foreach (var comp in tpl.components)
//                    {
//                        string type = comp.type?.ToString()?.ToUpperInvariant();

//                        if (type == "BODY")
//                            body = comp.text?.ToString() ?? "";

//                        if (type == "HEADER" && (comp.format?.ToString()?.ToUpperInvariant() == "IMAGE"))
//                            hasImageHeader = true;

//                        if (type == "BUTTONS")
//                        {
//                            foreach (var b in comp.buttons)
//                            {
//                                try
//                                {
//                                    string btnType = b.type?.ToString()?.ToUpperInvariant() ?? "";
//                                    string text = b.text?.ToString() ?? "";
//                                    int index = buttons.Count;

//                                    string subType = btnType switch
//                                    {
//                                        "URL" => "url",
//                                        "PHONE_NUMBER" => "voice_call",
//                                        "QUICK_REPLY" => "quick_reply",
//                                        "COPY_CODE" => "copy_code",
//                                        "CATALOG" => "catalog",
//                                        "FLOW" => "flow",
//                                        "REMINDER" => "reminder",
//                                        "ORDER_DETAILS" => "order_details",
//                                        _ => "unknown"
//                                    };

//                                    string? param = b.url != null ? b.url.ToString()
//                                                 : b.phone_number != null ? b.phone_number.ToString()
//                                                 : b.coupon_code != null ? b.coupon_code.ToString()
//                                                 : b.flow_id != null ? b.flow_id.ToString()
//                                                 : null;

//                                    bool hasExample = b.example != null;
//                                    bool isDynamic = hasExample && Regex.IsMatch(b.example.ToString(), @"\{\{[0-9]+\}\}");
//                                    bool requiresParam = new[] { "url", "flow", "copy_code", "catalog", "reminder" }.Contains(subType);
//                                    bool needsRuntimeValue = requiresParam && isDynamic;
//                                    if (subType == "unknown" || (param == null && needsRuntimeValue)) continue;

//                                    buttons.Add(new ButtonMetadataDto
//                                    {
//                                        Text = text,
//                                        Type = btnType,
//                                        SubType = subType,
//                                        Index = index,
//                                        ParameterValue = param ?? ""
//                                    });
//                                }
//                                catch (Exception ex)
//                                { _log.LogWarning(ex, "Button parse failed for template {Name}", (string)name); }
//                            }
//                        }
//                    }

//                    int placeholders = Regex.Matches(body ?? "", "{{(.*?)}}").Count;
//                    var raw = JsonConvert.SerializeObject(tpl);

//                    items.Add(new TemplateCatalogItem(
//                        Name: name,
//                        Language: language,
//                        Body: body,
//                        PlaceholderCount: placeholders,
//                        HasImageHeader: hasImageHeader,
//                        Buttons: buttons,
//                        Status: status,
//                        Category: tpl.category?.ToString(),
//                        ExternalId: tpl.id?.ToString(),
//                        RawJson: raw
//                    ));
//                }

//                next = parsed?.paging?.next?.ToString();
//            }

//            return items;
//        }

//        public async Task<TemplateCatalogItem?> GetByNameAsync(WhatsAppSettingEntity s, string templateName, CancellationToken ct = default)
//            => (await ListAsync(s, ct)).FirstOrDefault(t => t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));
//    }
//}