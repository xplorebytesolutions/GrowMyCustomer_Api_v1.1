using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.TemplateModule.Abstractions;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class MetaTemplateClient : IMetaTemplateClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMetaCredentialsResolver _creds;
    private readonly ILogger<MetaTemplateClient> _log;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public MetaTemplateClient(
        IHttpClientFactory httpClientFactory,
        IMetaCredentialsResolver creds,
        ILogger<MetaTemplateClient> log)
    {
        _httpClientFactory = httpClientFactory;
        _creds = creds;
        _log = log;
    }

    public async Task<string> UploadMediaAsync(Guid businessId, string localPathOrUrl, string mediaType, CancellationToken ct = default)
    {
        // For template headers, Meta requires an "asset handle" (aka header_handle).
        // If the caller passes an already-generated handle, accept it.
        if (localPathOrUrl.StartsWith("handle:", StringComparison.OrdinalIgnoreCase))
            return localPathOrUrl.Substring("handle:".Length);

        // Otherwise, we expect the caller to have performed the Resumable Upload flow.
        throw new NotSupportedException(
            "Media headers require a Meta asset handle (header_handle). " +
            "Upload the media via Meta Resumable Upload API first and pass the resulting handle " +
            "(prefix with 'handle:' if you want to bypass this guard).");
    }

    public async Task<MetaTemplateCallResult> CreateTemplateAsync(
        Guid businessId,
        string name,
        string category,
        string language,
        object componentsPayload,
        object examplesPayload,
        CancellationToken ct = default)
    {
        try
        {
            // Resolve real credentials from your WhatsAppSettings-backed resolver.
            // Your resolver can return either:
            //  - GraphBaseUrl + GraphVersion (e.g., https://graph.facebook.com + v21.0), or
            //  - ApiUrl that already includes a version (e.g., https://graph.facebook.com/v21.0)
            var c = await _creds.ResolveAsync(businessId, ct);

            // Build base URL robustly whether version is present or not.
            // If your resolver sets GraphVersion to "", pathPart becomes "".
            var baseRoot = (c.GraphBaseUrl ?? "").TrimEnd('/');
            var versionPart = string.IsNullOrWhiteSpace(c.GraphVersion) ? "" : "/" + c.GraphVersion.Trim('/');
            var baseWithVersion = $"{baseRoot}{versionPart}/";

            var client = _httpClientFactory.CreateClient("meta-graph");
            client.BaseAddress = new Uri(baseWithVersion);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);

            // Per Meta docs: POST /{WABA_ID}/message_templates
            // Body: name, category, language, components (examples for variables embedded via BODY.example).
            var payload = new
            {
                name,
                category,
                // allow_category_change = true, // potentially causing 100/2388299
                language,
                components = componentsPayload
            };

            var jsonPayload = JsonSerializer.Serialize(payload, JsonOpts);
            _log.LogInformation("Meta Create Payload: {Payload}", jsonPayload);

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync($"{c.WabaId}/message_templates", content, ct);

            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                _log.LogInformation("Meta create template OK | WABA {WabaId} | name {Name} | lang {Lang}", c.WabaId, name, language);
                return new MetaTemplateCallResult(true, null);
            }

            // Try to parse Meta error for clearer diagnostics.
            string friendlyError = ExtractMetaError(body, (int)resp.StatusCode);

            // Common statuses:
            // 400: invalid payload (components/examples/name)
            // 401/403: auth/permission
            // 409: duplicate name+language
            // 429/5xx: rate limiting / transient errors
            _log.LogWarning("Meta create failed ({StatusCode} {Status}): {Error}",
                (int)resp.StatusCode, resp.StatusCode, friendlyError);

            return new MetaTemplateCallResult(false, friendlyError);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Meta create template threw.");
            return new MetaTemplateCallResult(false, ex.Message);
        }
    }

    public Task<int> SyncTemplatesAsync(Guid businessId, CancellationToken ct = default)
        => Task.FromResult(0);

    public async Task<(Stream? ValidStream, string? ContentType)> GetMediaStreamAsync(Guid businessId, string mediaId, CancellationToken ct = default)
    {
        try
        {
            var c = await _creds.ResolveAsync(businessId, ct);
            var baseRoot = (c.GraphBaseUrl ?? "").TrimEnd('/');
            var versionPart = string.IsNullOrWhiteSpace(c.GraphVersion) ? "" : "/" + c.GraphVersion.Trim('/');
            var baseWithVersion = $"{baseRoot}{versionPart}/";

            var client = _httpClientFactory.CreateClient("meta-graph");
            client.BaseAddress = new Uri(baseWithVersion);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);

            // 1. Get Media Info to find URL
            var infoResp = await client.GetAsync(mediaId, ct);
            if (!infoResp.IsSuccessStatusCode)
            {
                var err = await infoResp.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Meta Get Media Info failed: {Code} {Body}", infoResp.StatusCode, err);
                return (null, null);
            }

            var infoJson = await infoResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(infoJson);
            if (!doc.RootElement.TryGetProperty("url", out var u) || u.ValueKind != JsonValueKind.String)
            {
                 _log.LogWarning("Meta Media Info response missing 'url' property.");
                 return (null, null);
            }

            var downloadUrl = u.GetString();
            if (string.IsNullOrEmpty(downloadUrl))
                return (null, null);

            // 2. Download Media Content
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);

            var downloadResp = await client.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!downloadResp.IsSuccessStatusCode)
            {
                 _log.LogWarning("Meta Download Media failed: {Code}", downloadResp.StatusCode);
                 return (null, null);
            }

            var stream = await downloadResp.Content.ReadAsStreamAsync(ct);
            var contentType = downloadResp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            
            return (stream, contentType);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Exception in GetMediaStreamAsync");
            return (null, null);
        }
    }

   

    // ───────────────────────── helpers ─────────────────────────

    private static string ExtractMetaError(string responseBody, int statusCode)
    {
        try
        {
            // Meta error shape: { "error": { "message": "...", "type": "...", "code": 0, "error_subcode": 0, "fbtrace_id": "..." } }
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                var type = err.TryGetProperty("type", out var t) ? t.GetString() : null;
                var code = err.TryGetProperty("code", out var c) ? c.GetInt32() : (int?)null;
                var sub = err.TryGetProperty("error_subcode", out var s) ? s.GetInt32() : (int?)null;
                var fb = err.TryGetProperty("fbtrace_id", out var f) ? f.GetString() : null;

                var userTitle = err.TryGetProperty("error_user_title", out var ut) ? ut.GetString() : null;
                var userMsg = err.TryGetProperty("error_user_msg", out var um) ? um.GetString() : null;
                var isTransient = err.TryGetProperty("is_transient", out var it) && it.ValueKind == JsonValueKind.True;
                var errorData = err.TryGetProperty("error_data", out var ed) ? ed.GetRawText() : null;

                var parts = new List<string>
                {
                    $"Meta error: {(type ?? "N/A")}",
                    $"code {(code?.ToString() ?? "N/A")}{(sub.HasValue ? $" subcode {sub}" : "")}",
                    $"message: {msg ?? "N/A"}",
                    $"fbtrace_id: {fb ?? "N/A"}"
                };

                if (!string.IsNullOrWhiteSpace(userTitle)) parts.Add($"user_title: {userTitle}");
                if (!string.IsNullOrWhiteSpace(userMsg)) parts.Add($"user_msg: {userMsg}");
                if (!string.IsNullOrWhiteSpace(errorData) && errorData != "null") parts.Add($"error_data: {errorData}");
                if (isTransient) parts.Add("transient: true");

                return string.Join(" | ", parts);
            }
        }
        catch
        {
            // ignore JSON parse errors, fall back to raw body
        }

        // Fallback to raw text if not JSON or unexpected
        return $"HTTP {statusCode}: {responseBody}";
    }

    public async Task<bool> DeleteTemplateAsync(Guid businessId, string name, string language, CancellationToken ct = default)
    {
        var c = await _creds.ResolveAsync(businessId, ct);
        var client = _httpClientFactory.CreateClient("meta-graph");
        client.BaseAddress = new Uri($"{c.GraphBaseUrl.TrimEnd('/')}/{c.GraphVersion.Trim('/')}/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);

        // Graph supports DELETE on /{wabaId}/message_templates with query params name & language
        var url = $"{c.WabaId}/message_templates?name={Uri.EscapeDataString(name)}&language={Uri.EscapeDataString(language)}";
        using var resp = await client.DeleteAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
        {
            _log.LogInformation("Meta delete template OK | WABA {WabaId} | name {Name} | lang {Lang}", c.WabaId, name, language);
            return true;
        }

        // 404/400 means already gone or not found; treat as success from UX standpoint
        if ((int)resp.StatusCode == 404 || (int)resp.StatusCode == 400)
        {
            _log.LogWarning("Meta delete returned {Code} but treating as success. Body: {Body}", resp.StatusCode, body);
            return true;
        }

        _log.LogWarning("Meta delete failed ({Code}): {Body}", resp.StatusCode, body);
        return false;
    }

}
