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

    public async Task<bool> CreateTemplateAsync(
        Guid businessId,
        string name,
        string category,
        string language,
        object componentsPayload,
        object examplesPayload,
        CancellationToken ct = default)
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
        // Body: name, category, language, components (examples for variables embedded via BODY).
        var payload = new
        {
            name,
            category,
            allow_category_change = true, // helps reduce rejections due to miscategorization
            language,
            components = componentsPayload
            // NOTE: If you later need top-level examples, include them here per the latest API version.
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        using var resp = await client.PostAsync($"{c.WabaId}/message_templates", content, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
        {
            _log.LogInformation("Meta create template OK | WABA {WabaId} | name {Name} | lang {Lang}", c.WabaId, name, language);
            return true;
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

        return false;
    }

    public Task<int> SyncTemplatesAsync(Guid businessId, CancellationToken ct = default)
        => Task.FromResult(0);

   

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

                return $"Meta error: {(type ?? "N/A")} | code {(code?.ToString() ?? "N/A")}" +
                       $"{(sub.HasValue ? $" subcode {sub}" : "")} | message: {msg ?? "N/A"} | fbtrace_id: {fb ?? "N/A"}";
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
