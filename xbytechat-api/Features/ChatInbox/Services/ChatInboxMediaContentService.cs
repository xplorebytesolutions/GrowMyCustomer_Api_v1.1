using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.TemplateModule.Abstractions;

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// Streams WhatsApp media bytes from Meta Cloud API without persisting on our servers.
    /// This exists to support secure agent-side previews (the browser cannot attach Bearer tokens to &lt;img src&gt;).
    /// </summary>
    public sealed class ChatInboxMediaContentService : IChatInboxMediaContentService
    {
        private const string DefaultGraphVersion = "v22.0";

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMetaCredentialsResolver _metaCreds;
        private readonly ILogger<ChatInboxMediaContentService> _logger;

        public ChatInboxMediaContentService(
            IHttpClientFactory httpFactory,
            IMetaCredentialsResolver metaCreds,
            ILogger<ChatInboxMediaContentService> logger)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _metaCreds = metaCreds ?? throw new ArgumentNullException(nameof(metaCreds));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(Stream Stream, string ContentType)> DownloadFromWhatsAppAsync(
            Guid businessId,
            string mediaId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (string.IsNullOrWhiteSpace(mediaId)) throw new ArgumentException("mediaId is required.", nameof(mediaId));

            var creds = await _metaCreds.ResolveAsync(businessId, ct).ConfigureAwait(false);
            var graphBase = (creds.GraphBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var graphVersion = string.IsNullOrWhiteSpace(creds.GraphVersion)
                ? DefaultGraphVersion
                : creds.GraphVersion!.Trim().Trim('/');

            if (string.IsNullOrWhiteSpace(graphBase))
                throw new InvalidOperationException("WhatsApp Graph API base URL is missing.");

            var token = (creds.AccessToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("WhatsApp access token is missing for this business.");

            // Step 1: resolve the temporary media URL from Meta (GET /{media-id})
            var metaUrl = $"{graphBase}/{graphVersion}/{mediaId.Trim()}";

            using var metaReq = new HttpRequestMessage(HttpMethod.Get, metaUrl);
            metaReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            metaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var client = _httpFactory.CreateClient("wa:meta_cloud");
            using var metaResp = await client.SendAsync(metaReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var metaBody = await metaResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!metaResp.IsSuccessStatusCode)
            {
                var clean = TryGetMetaErrorMessage(metaBody) ?? "Failed to resolve WhatsApp media URL.";
                _logger.LogWarning(
                    "ChatInbox media resolve failed. BusinessId={BusinessId} MediaId={MediaId} Status={Status} MetaError={MetaError}",
                    businessId,
                    mediaId,
                    (int)metaResp.StatusCode,
                    clean);
                throw new InvalidOperationException(clean);
            }

            var downloadUrl = TryGetMetaMediaUrl(metaBody);
            var mimeType = TryGetMetaMediaMimeType(metaBody) ?? "application/octet-stream";

            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new InvalidOperationException("Meta did not return a download URL for this media.");

            // Step 2: stream the media bytes from the resolved URL (Bearer token required)
            var fileReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            fileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var fileResp = await client.SendAsync(fileReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!fileResp.IsSuccessStatusCode)
            {
                var body = await fileResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                fileResp.Dispose();

                var clean = TryGetMetaErrorMessage(body) ?? "Failed to download WhatsApp media.";
                _logger.LogWarning(
                    "ChatInbox media download failed. BusinessId={BusinessId} MediaId={MediaId} Status={Status} MetaError={MetaError}",
                    businessId,
                    mediaId,
                    (int)fileResp.StatusCode,
                    clean);
                throw new InvalidOperationException(clean);
            }

            var contentType =
                fileResp.Content.Headers.ContentType?.MediaType ??
                mimeType ??
                "application/octet-stream";

            Stream stream;
            try
            {
                stream = await fileResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                fileResp.Dispose();
                throw;
            }

            return (new ResponseDisposingStream(stream, fileResp), contentType);
        }

        private sealed class ResponseDisposingStream : Stream
        {
            private readonly Stream _inner;
            private readonly HttpResponseMessage _resp;

            public ResponseDisposingStream(Stream inner, HttpResponseMessage resp)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _resp = resp ?? throw new ArgumentNullException(nameof(resp));
            }

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
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                _inner.ReadAsync(buffer, offset, count, cancellationToken);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                _inner.ReadAsync(buffer, cancellationToken);
            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
                _inner.CopyToAsync(destination, bufferSize, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _inner.Dispose(); } catch { /* ignore */ }
                    try { _resp.Dispose(); } catch { /* ignore */ }
                }
                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                try { await _inner.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
                try { _resp.Dispose(); } catch { /* ignore */ }
            }
        }

        private static string? TryGetMetaMediaUrl(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (doc.RootElement.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                    return urlProp.GetString();
            }
            catch { /* ignore */ }

            return null;
        }

        private static string? TryGetMetaMediaMimeType(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (doc.RootElement.TryGetProperty("mime_type", out var mimeProp) && mimeProp.ValueKind == JsonValueKind.String)
                    return mimeProp.GetString();
            }
            catch { /* ignore */ }

            return null;
        }

        private static string? TryGetMetaErrorMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                if (!doc.RootElement.TryGetProperty("error", out var err) || err.ValueKind != JsonValueKind.Object)
                    return null;

                if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    return msg.GetString();
            }
            catch { /* ignore */ }

            return null;
        }
    }
}

