using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.TemplateModule.Abstractions;

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// Uploads Inbox attachments to WhatsApp Cloud API media storage and returns a media_id.
    /// We intentionally do NOT store files on our server.
    /// </summary>
    public sealed class ChatInboxMediaUploadService : IChatInboxMediaUploadService
    {
        private const string DefaultGraphVersion = "v22.0";

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMetaCredentialsResolver _metaCreds;
        private readonly ILogger<ChatInboxMediaUploadService> _logger;

        public ChatInboxMediaUploadService(
            IHttpClientFactory httpFactory,
            IMetaCredentialsResolver metaCreds,
            ILogger<ChatInboxMediaUploadService> logger)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _metaCreds = metaCreds ?? throw new ArgumentNullException(nameof(metaCreds));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> UploadToWhatsAppAsync(
            Guid businessId,
            string? phoneNumberId,
            IFormFile file,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (file == null) throw new ArgumentNullException(nameof(file));

            var creds = await _metaCreds.ResolveAsync(businessId, ct).ConfigureAwait(false);
            var graphBase = (creds.GraphBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var graphVersion = string.IsNullOrWhiteSpace(creds.GraphVersion)
                ? DefaultGraphVersion
                : creds.GraphVersion!.Trim().Trim('/');

            if (string.IsNullOrWhiteSpace(graphBase))
                throw new InvalidOperationException("WhatsApp Graph API base URL is missing.");

            var pnid = string.IsNullOrWhiteSpace(phoneNumberId)
                ? (creds.PhoneNumberId ?? string.Empty).Trim()
                : phoneNumberId.Trim();

            if (string.IsNullOrWhiteSpace(pnid))
                throw new InvalidOperationException("WhatsApp phone_number_id is missing for this business.");

            var url = $"{graphBase}/{graphVersion}/{pnid}/media";

            using var fileStream = file.OpenReadStream();
            using var mp = new MultipartFormDataContent();

            mp.Add(new StringContent("whatsapp"), "messaging_product");
            mp.Add(new StringContent(file.ContentType ?? "application/octet-stream"), "type");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            mp.Add(fileContent, "file", file.FileName ?? "upload.bin");

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = mp };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var token = (creds.AccessToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("WhatsApp access token is missing for this business.");

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var client = _httpFactory.CreateClient("wa:meta_cloud");
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var clean = TryGetMetaErrorMessage(body) ?? "WhatsApp media upload failed.";
                _logger.LogWarning(
                    "ChatInbox media upload failed. BusinessId={BusinessId} PhoneNumberId={PhoneNumberId} Status={Status} MetaError={MetaError}",
                    businessId,
                    pnid,
                    (int)resp.StatusCode,
                    clean);
                throw new InvalidOperationException(clean);
            }

            var mediaId = TryGetMediaId(body);
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                _logger.LogWarning(
                    "ChatInbox media upload succeeded but media id missing. BusinessId={BusinessId} PhoneNumberId={PhoneNumberId}",
                    businessId,
                    pnid);
                throw new InvalidOperationException("WhatsApp media upload succeeded but no media id was returned.");
            }

            return mediaId;
        }

        private static string? TryGetMediaId(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    return idProp.GetString();
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

