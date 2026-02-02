using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.Config;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class MetaUploadService : IMetaUploadService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMetaCredentialsResolver _creds;
    private readonly UploadLimitsOptions _opts;
    private readonly ILogger<MetaUploadService> _logger;

    public MetaUploadService(
        IHttpClientFactory httpFactory,
        IMetaCredentialsResolver creds,
        IOptions<UploadLimitsOptions> opts,
        ILogger<MetaUploadService> logger)
    {
        _httpFactory = httpFactory;
        _creds = creds;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<HeaderUploadResult> UploadHeaderAsync(
        Guid businessId,
        HeaderMediaType mediaType,
        Stream? fileStream,
        string? fileName,
        string? sourceUrl,
        CancellationToken ct = default)
    {
        if ((fileStream is null && string.IsNullOrWhiteSpace(sourceUrl)) ||
            (fileStream is not null && !string.IsNullOrWhiteSpace(sourceUrl)))
            throw new ArgumentException("Provide either a file or a sourceUrl, not both.");

        // If sourceUrl provided, download then proceed
        if (fileStream is null && sourceUrl is not null)
        {
            using var http = new HttpClient();
            using var resp = await http.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var tmp = new MemoryStream();
            await resp.Content.CopyToAsync(tmp, ct);
            tmp.Position = 0;

            var mime = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fname = Path.GetFileName(new Uri(sourceUrl).LocalPath);
            return await UploadCoreAsync(businessId, mediaType, tmp, fname, mime, ct);
        }

        // File provided
        if (fileStream is not null)
        {
            var mime = GuessMime(fileName, mediaType);
            return await UploadCoreAsync(businessId, mediaType, fileStream, fileName ?? "upload.bin", mime, ct);
        }

        throw new InvalidOperationException("No file or URL provided.");
    }

    private async Task<HeaderUploadResult> UploadCoreAsync(
        Guid businessId,
        HeaderMediaType mediaType,
        Stream content,
        string fileName,
        string mime,
        CancellationToken ct)
    {
        // Ensure we can read length + seek (so we can retry Path B after Path A)
        if (!content.CanSeek)
        {
            var mem = new MemoryStream();
            await content.CopyToAsync(mem, ct);
            mem.Position = 0;
            content = mem;
        }

        var size = content.Length;

        // Size/MIME guards
        switch (mediaType)
        {
            case HeaderMediaType.IMAGE:
                if (size > _opts.ImageMaxBytes) throw new InvalidOperationException("Image too large.");
                if (!_opts.AllowedImageMime.Contains(mime, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Invalid image MIME: {mime}");
                break;

            case HeaderMediaType.VIDEO:
                if (size > _opts.VideoMaxBytes) throw new InvalidOperationException("Video too large.");
                if (!_opts.AllowedVideoMime.Contains(mime, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Invalid video MIME: {mime}");
                break;

            case HeaderMediaType.DOCUMENT:
                if (size > _opts.DocumentMaxBytes) throw new InvalidOperationException("Document too large.");
                if (!_opts.AllowedDocMime.Contains(mime, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Invalid document MIME: {mime}");
                break;
        }

        // ── STUB MODE ─────────────────────────────────────────────────────────────
        if (_opts.UseStubHandle)
        {
            var handleStub = $"{(int)mediaType}:{Guid.NewGuid():N}".Insert(1, "::");
            return new HeaderUploadResult(handleStub, mime, size, IsStub: true);
        }

        // ── REAL MODE: Meta Resumable Upload ─────────────────────────────────────
        var c = await _creds.ResolveAsync(businessId, ct);

        var baseRoot = (c.GraphBaseUrl ?? "").TrimEnd('/');
        var versionPart = string.IsNullOrWhiteSpace(c.GraphVersion) ? "" : "/" + c.GraphVersion.Trim('/');
        var baseWithVersion = $"{baseRoot}{versionPart}";

        using var client = _httpFactory.CreateClient("meta-graph");
        client.BaseAddress = new Uri(baseWithVersion + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c.AccessToken);

        // Meta expects IMAGE|VIDEO|DOCUMENT
        // Meta expects the MIME type (e.g. "image/jpeg"), NOT just "IMAGE".
        string fileTypeParam = mime;

        _logger.LogInformation("Starting Meta Upload. WABA: {WabaId}, Type: {Type}, Mime: {Mime}, Size: {Size}", 
             c.WabaId, mediaType, mime, size);


        // Path A: uploads session
        // Primary method for Templates.
        // Returns a handle (4::...) OR a resolved numeric ID if possible.
        var handle = await TryPathA_UploadsEndpointAsync(client, c.WabaId, fileTypeParam, new NonDisposableStream(content), fileName, mime, size, ct);
        if (!string.IsNullOrWhiteSpace(handle))
        {
            return new HeaderUploadResult(handle!, mime, size, false);
        }

        // Rewind
        try { content.Position = 0; } catch { }

        // Path B: Phased (Fallback)
        var handleB = await TryPathB_PhasedUploadAsync(client, c.WabaId, fileTypeParam, new NonDisposableStream(content), fileName, mime, size, ct);
        if (!string.IsNullOrWhiteSpace(handleB))
             return new HeaderUploadResult(handleB!, mime, size, false);
             
        // Path C: Classic (Numeric ID) - Low Priority / Fallback
        // The numeric ID returned by this path is currently REJECTED by Template API.
        // We keep it as a last resort or if future API versions accept it.
        try 
        {
             var targetId = !string.IsNullOrWhiteSpace(c.PhoneNumberId) ? c.PhoneNumberId : c.WabaId;
             var handleC = await TryPathC_ClassicMediaEndpointAsync(client, targetId!, new NonDisposableStream(content), fileName, mime, size, ct);
             if (!string.IsNullOrWhiteSpace(handleC))
                return new HeaderUploadResult(handleC!, mime, size, false);
        }
        catch (Exception ex)
        {
             _logger.LogWarning(ex, "Meta Upload Path C (Classic) failed.");
        }

        // Path B Retry (just in case flow falls here unexpectedly)
        handle = await TryPathB_PhasedUploadAsync(client, c.WabaId, fileTypeParam, content, fileName, mime, size, ct);
        if (!string.IsNullOrWhiteSpace(handle))
        {
            return new HeaderUploadResult(handle!, mime, size, false);
        }

        throw new InvalidOperationException("Meta upload did not return an asset handle. Check app permissions and the response logs.");
    }

    // ── Path A: WABA uploads session (single-shot Content-Range) ────────────────
    private async Task<string?> TryPathA_UploadsEndpointAsync(
        HttpClient client,
        string wabaId,
        string fileTypeParam,
        Stream content,
        string fileName,
        string mime,
        long size,
        CancellationToken ct)
    {
        // INIT
        // Params must be in URL query string for app/uploads AND Body to be safe.
        // We ensure messaging_product=whatsapp is present in both.
        
        var safeFileName = (fileName ?? "upload.bin").Replace(" ", "_");
        // Body (FormUrlEncoded)
        var initPayload = new Dictionary<string, string>
        {
            ["file_name"] = safeFileName,
            ["file_length"] = size.ToString(),
            ["file_type"] = fileTypeParam,
            ["messaging_product"] = "whatsapp"
        };
        
        // Query String (Extra safety for scoping)
        // Note: app/uploads sometimes ignores body for init, so query string is crucial.
        var initUrl = $"{client.BaseAddress}app/uploads?messaging_product=whatsapp";
        
        using var initResp = await client.PostAsync(initUrl, new FormUrlEncodedContent(initPayload), ct);
        var initText = await initResp.Content.ReadAsStringAsync(ct);
        
        if (!initResp.IsSuccessStatusCode)
        {
             _logger.LogWarning("Meta Upload Path A (Init) Failed. Status: {Status}, Body: {Body}", initResp.StatusCode, initText);
             return null;
        }

        var initJson = SafeParse(initText);
        var uploadId = initJson.TryGetValue("id", out var idObj) ? idObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(uploadId)) return null;

        // TRANSFER (single range)
        // Use absolute URI to prevent "scheme not supported" error on "upload:xxx"
        using (var req = new HttpRequestMessage(HttpMethod.Post, $"{client.BaseAddress}{uploadId}?messaging_product=whatsapp"))
        {
            req.Content = new StreamContent(content);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = size;
            req.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{size - 1}/{size}");

            using var upResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var upText = await upResp.Content.ReadAsStringAsync(ct);
            if (!upResp.IsSuccessStatusCode)
            {
                 _logger.LogWarning("Meta Upload Path A (Transfer) Failed. Status: {Status}, Body: {Body}", upResp.StatusCode, upText);
                 return null;
            }
        }

        // FINISH
        var finPayload = new Dictionary<string, string> { ["finish"] = "true" };
        var finUrl = $"{client.BaseAddress}{uploadId}?messaging_product=whatsapp";
        using var finResp = await client.PostAsync(finUrl, new FormUrlEncodedContent(finPayload), ct);
        var finText = await finResp.Content.ReadAsStringAsync(ct);
        
        if (!finResp.IsSuccessStatusCode)
        {
             _logger.LogWarning("Meta Upload Path A (Finish) Failed. Status: {Status}, Body: {Body}", finResp.StatusCode, finText);
             return null;
        }

        // Log success body to debug handle extraction
        _logger.LogInformation("Meta Upload Path A (Finish) Success. Body: {Body}", finText);

        var finJson = SafeParse(finText);
        if (TryExtractHandle(finJson, out var handle))
            return handle;

        return null;
    }

    // ── Path B: classic phased upload (start/transfer/finish) ───────────────────
    private async Task<string?> TryPathB_PhasedUploadAsync(
        HttpClient client,
        string wabaId,
        string fileTypeParam,
        Stream content,
        string fileName,
        string mime,
        long size,
        CancellationToken ct)
    {
        // START
        var startPayload = new Dictionary<string, string>
        {
            ["upload_phase"] = "start",
            ["file_type"] = fileTypeParam,
            ["file_length"] = size.ToString(),
            ["messaging_product"] = "whatsapp"
        };
        using var startResp = await client.PostAsync("app/uploads", new FormUrlEncodedContent(startPayload), ct);
        var startText = await startResp.Content.ReadAsStringAsync(ct);
        
        if (!startResp.IsSuccessStatusCode)
        {
             _logger.LogWarning("Meta Upload Path B (Start) Failed. Status: {Status}, Body: {Body}", startResp.StatusCode, startText);
             return null;
        }

        var startJson = SafeParse(startText);
        var sessionId = startJson.TryGetValue("id", out var idObj) ? idObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        // TRANSFER
        var transferSep = sessionId.Contains('?') ? "&" : "?";
        using (var req = new HttpRequestMessage(HttpMethod.Post, $"{client.BaseAddress}{sessionId}{transferSep}upload_phase=transfer&messaging_product=whatsapp"))
        {
            req.Content = new StreamContent(content);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = size;
            req.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{size - 1}/{size}");

            using var transferResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var transferText = await transferResp.Content.ReadAsStringAsync(ct);
            if (!transferResp.IsSuccessStatusCode)
            {
                 _logger.LogWarning("Meta Upload Path B (Transfer) Failed. Status: {Status}, Body: {Body}", transferResp.StatusCode, transferText);
                 return null;
            }
        }

        // FINISH
        var finishSep = sessionId.Contains('?') ? "&" : "?";
        var finishUrl = $"{client.BaseAddress}{sessionId}{finishSep}upload_phase=finish&messaging_product=whatsapp";
        using (var req = new HttpRequestMessage(HttpMethod.Post, finishUrl))
        {
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["confirm"] = "true" });
            using var finishResp = await client.SendAsync(req, ct);
            var finishText = await finishResp.Content.ReadAsStringAsync(ct);
            
            if (!finishResp.IsSuccessStatusCode)
            {
                 _logger.LogWarning("Meta Upload Path B (Finish) Failed. Status: {Status}, Body: {Body}", finishResp.StatusCode, finishText);
                 return null;
            }

            // Log success body to debug handle extraction
            _logger.LogInformation("Meta Upload Path B (Finish) Success. Body: {Body}", finishText);

            var finJson = SafeParse(finishText);
            if (TryExtractHandle(finJson, out var handle))
                return handle;
        }

        return null;
    }

    // ── JSON helpers ────────────────────────────────────────────────────────────
    private static Dictionary<string, object?> SafeParse(string json)
    {
        try
        {
            var root = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            return root ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static bool TryExtractHandle(Dictionary<string, object?> json, out string? handle)
    {
        handle = null;
        if (json == null) return false;

        // Keys to check in order of preference
        // Added "id" because some Meta regions return the asset handle in the "id" field during Finish.
        var keys = new[] { "h", "handle", "asset_handle", "id" };

        foreach (var key in keys)
        {
            if (json.TryGetValue(key, out var val) && val is not null)
            {
                var s = val.ToString();
                // If it's a JsonElement, GetString() is safer/cleaner than ToString()
                if (val is JsonElement el)
                {
                    if (el.ValueKind == JsonValueKind.String)
                        s = el.GetString();
                    else 
                        s = el.ToString(); 
                }

                if (!string.IsNullOrWhiteSpace(s))
                {
                    handle = s;
                    return true;
                }
            }
        }

        // nested { "result": ... }
        if (json.TryGetValue("result", out var resObj) && resObj is JsonElement resEl && resEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in keys)
            {
                if (resEl.TryGetProperty(key, out var prop))
                {
                     var s = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
                     if (!string.IsNullOrWhiteSpace(s))
                     {
                         handle = s;
                         return true;
                     }
                }
            }
        }

        return false;
    }

    // ── Path C: Classic Media Endpoint (POST /<targetId>/media) ────────────────
    private async Task<string?> TryPathC_ClassicMediaEndpointAsync(
        HttpClient client,
        string targetId,
        Stream content,
        string fileName,
        string mime,
        long size,
        CancellationToken ct)
    {
        // 25MB limit hard check (though server might reject earlier)
        // If > 25MB, skip immediately to fallback to resumable
        if (size > 25 * 1024 * 1024) return null;

        using var form = new MultipartFormDataContent(); 
        
        // Correct field: messaging_product="whatsapp"
        // We need to be careful with MultipartFormDataContent.Add(content, name, fileName)
        
        var mp = new MultipartFormDataContent();
        mp.Add(new StringContent("whatsapp"), "messaging_product");
        
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
        mp.Add(fileContent, "file", fileName);

        using var resp = await client.PostAsync($"{targetId}/media", mp, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
             _logger.LogWarning("Meta Upload Path C (Classic) Failed. Status: {Status}, Body: {Body}", resp.StatusCode, text);
             return null;
        }

        _logger.LogInformation("Meta Upload Path C (Classic) Success. Body: {Body}", text);
        var json = SafeParse(text);

        // Classic endpoint returns { "id": "1234..." }
        if (json.TryGetValue("id", out var val) && val is not null)
        {
             return val.ToString();
        }

        return null;
    }

    // ── Resolve Handle (GET handle -> ID) ───────────────────────────────────────
    // ── Resolve Media ID (Handle or Session -> ID) ──────────────────────────────
    private async Task<string> ResolveMediaIdAsync(HttpClient client, string handle, string? sessionId, CancellationToken ct)
    {
        // Strategy 1: GET {handle}
        try
        {
            // Handle might contain chars that need encoding for URL path?
            // "4:..." usually safe, but let's be sure.
            // Actually, HttpClient doesn't like encoded colons in path sometimes.
            // But 4:: is weird. Let's try direct first.
            
            var reqUrl = handle;
            // If handle contains crazy chars, Uri creation might fail.
            // Let's try just GET.
            
            var resp = await client.GetAsync(reqUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                var jsonText = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("Resolve(Handle) Success: {Handle} -> {Body}", handle, jsonText);
                var json = SafeParse(jsonText);
                if (json.TryGetValue("id", out var val) && val is not null) return val.ToString()!;
            }
            else
            {
                 _logger.LogWarning("Resolve(Handle) Failed: {Handle} Status: {Status}", handle, resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resolve(Handle) Exception: {Handle}", handle);
        }

        // Strategy 2: If sessionId provided, GET {sessionId}?fields=id,h
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            try
            {
                var resp = await client.GetAsync($"{sessionId}?fields=id,handle,status", ct);
                if (resp.IsSuccessStatusCode)
                {
                    var jsonText = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogInformation("Resolve(Session) Success: {SessionId} -> {Body}", sessionId, jsonText);
                    var json = SafeParse(jsonText);
                    if (json.TryGetValue("id", out var val) && val is not null) return val.ToString()!;
                }
                else
                {
                     _logger.LogWarning("Resolve(Session) Failed: {SessionId} Status: {Status}", sessionId, resp.StatusCode);
                }
            }
             catch (Exception ex)
            {
                _logger.LogError(ex, "Resolve(Session) Exception: {SessionId}", sessionId);
            }
        }

        return handle;
    }

    // ── MIME guessing ───────────────────────────────────────────────────────────
    private static string GuessMime(string? fileName, HeaderMediaType mediaType)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        if (mediaType == HeaderMediaType.IMAGE)
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

        if (mediaType == HeaderMediaType.VIDEO)
            return ext switch
            {
                ".3gp" or ".3gpp" => "video/3gpp",
                _ => "video/mp4"
            };

        if (mediaType == HeaderMediaType.DOCUMENT)
            return ext switch
            {
                ".pdf" => "application/pdf",
                _ => "application/pdf"
            };

        return "application/octet-stream";
    }
    // ── Stream Wrapper to prevent disposal ─────────────────────────────────────
    private class NonDisposableStream : Stream
    {
        private readonly Stream _inner;
        public NonDisposableStream(Stream inner) => _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        
        // Key: Do NOT dispose the inner stream
        protected override void Dispose(bool disposing) { /* no-op */ }
    }
}
