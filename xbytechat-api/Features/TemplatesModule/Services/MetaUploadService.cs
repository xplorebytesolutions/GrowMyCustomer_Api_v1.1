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

    public MetaUploadService(
        IHttpClientFactory httpFactory,
        IMetaCredentialsResolver creds,
        IOptions<UploadLimitsOptions> opts)
    {
        _httpFactory = httpFactory;
        _creds = creds;
        _opts = opts.Value;
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
        string fileTypeParam = mediaType switch
        {
            HeaderMediaType.IMAGE => "IMAGE",
            HeaderMediaType.VIDEO => "VIDEO",
            HeaderMediaType.DOCUMENT => "DOCUMENT",
            _ => "DOCUMENT"
        };

        // Path A: uploads session
        var handle = await TryPathA_UploadsEndpointAsync(client, c.WabaId, fileTypeParam, content, fileName, mime, size, ct);
        if (!string.IsNullOrWhiteSpace(handle))
            return new HeaderUploadResult(handle!, mime, size, false);

        // Rewind before fallback (Path A already consumed the stream)
        try { content.Position = 0; } catch { /* ignore */ }

        // Path B: phased upload
        handle = await TryPathB_PhasedUploadAsync(client, c.WabaId, fileTypeParam, content, fileName, mime, size, ct);
        if (!string.IsNullOrWhiteSpace(handle))
            return new HeaderUploadResult(handle!, mime, size, false);

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
        var initPayload = new Dictionary<string, string>
        {
            ["file_type"] = fileTypeParam,
            ["file_length"] = size.ToString()
        };
        using var initResp = await client.PostAsync($"{wabaId}/uploads", new FormUrlEncodedContent(initPayload), ct);
        var initText = await initResp.Content.ReadAsStringAsync(ct);
        if (!initResp.IsSuccessStatusCode) return null;

        var initJson = SafeParse(initText);
        var uploadId = initJson.TryGetValue("id", out var idObj) ? idObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(uploadId)) return null;

        // TRANSFER (single range)
        using (var req = new HttpRequestMessage(HttpMethod.Post, uploadId))
        {
            req.Content = new StreamContent(content);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = size;
            req.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{size - 1}/{size}");

            using var upResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var upText = await upResp.Content.ReadAsStringAsync(ct);
            if (!upResp.IsSuccessStatusCode) return null;
        }

        // FINISH
        var finPayload = new Dictionary<string, string> { ["finish"] = "true" };
        using var finResp = await client.PostAsync(uploadId, new FormUrlEncodedContent(finPayload), ct);
        var finText = await finResp.Content.ReadAsStringAsync(ct);
        if (!finResp.IsSuccessStatusCode) return null;

        var finJson = SafeParse(finText);
        if (TryExtractHandle(finJson, out var handle)) return handle;

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
            ["file_length"] = size.ToString()
        };
        using var startResp = await client.PostAsync($"{wabaId}/uploads", new FormUrlEncodedContent(startPayload), ct);
        var startText = await startResp.Content.ReadAsStringAsync(ct);
        if (!startResp.IsSuccessStatusCode) return null;

        var startJson = SafeParse(startText);
        var sessionId = startJson.TryGetValue("id", out var idObj) ? idObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        // TRANSFER
        using (var req = new HttpRequestMessage(HttpMethod.Post, $"{sessionId}?upload_phase=transfer"))
        {
            req.Content = new StreamContent(content);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = size;
            req.Headers.TryAddWithoutValidation("Content-Range", $"bytes 0-{size - 1}/{size}");

            using var transferResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var transferText = await transferResp.Content.ReadAsStringAsync(ct);
            if (!transferResp.IsSuccessStatusCode) return null;
        }

        // FINISH
        using (var req = new HttpRequestMessage(HttpMethod.Post, $"{sessionId}?upload_phase=finish"))
        {
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["confirm"] = "true" });

            using var finishResp = await client.SendAsync(req, ct);
            var finishText = await finishResp.Content.ReadAsStringAsync(ct);
            if (!finishResp.IsSuccessStatusCode) return null;

            var finJson = SafeParse(finishText);
            if (TryExtractHandle(finJson, out var handle)) return handle;
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

        if (json.TryGetValue("h", out var h) && !string.IsNullOrWhiteSpace(h?.ToString()))
        {
            handle = h!.ToString();
            return true;
        }

        if (json.TryGetValue("handle", out var hh) && !string.IsNullOrWhiteSpace(hh?.ToString()))
        {
            handle = hh!.ToString();
            return true;
        }

        if (json.TryGetValue("asset_handle", out var ah) && !string.IsNullOrWhiteSpace(ah?.ToString()))
        {
            handle = ah!.ToString();
            return true;
        }

        // sometimes nested { "result": { "h": "4::..." } }
        if (json.TryGetValue("result", out var resObj) &&
            resObj is JsonElement el &&
            el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("h", out var h2) && h2.ValueKind == JsonValueKind.String)
            {
                handle = h2.GetString();
                return true;
            }
            if (el.TryGetProperty("handle", out var h3) && h3.ValueKind == JsonValueKind.String)
            {
                handle = h3.GetString();
                return true;
            }
            if (el.TryGetProperty("asset_handle", out var h4) && h4.ValueKind == JsonValueKind.String)
            {
                handle = h4.GetString();
                return true;
            }
        }

        return false;
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
            return "video/mp4";

        if (mediaType == HeaderMediaType.DOCUMENT)
            return ext switch
            {
                ".pdf" => "application/pdf",
                _ => "application/pdf"
            };

        return "application/octet-stream";
    }
}
