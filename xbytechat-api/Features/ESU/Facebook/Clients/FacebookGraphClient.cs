#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Contracts;
using xbytechat.api.Features.ESU.Facebook.Options;

namespace xbytechat.api.Features.ESU.Facebook.Clients
{
    internal sealed class FacebookGraphClient : IFacebookGraphClient
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        { PropertyNameCaseInsensitive = true, WriteIndented = false };

        private readonly HttpClient _http;
        private readonly ILogger<FacebookGraphClient> _log;
        private readonly FacebookOauthOptions _opt;
        private readonly IFacebookTokenService _tokens;

        public FacebookGraphClient(
            HttpClient http,
            IOptions<FacebookOauthOptions> opt,
            IFacebookTokenService tokens,
            ILogger<FacebookGraphClient> log)
        {
            _http = http;
            _opt = opt.Value;
            _tokens = tokens;
            _log = log;
        }

        public async Task<T> GetAsync<T>(
            Guid businessId,
            string path,
            IDictionary<string, string?>? query = null,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("businessId is required", nameof(businessId));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required", nameof(path));

            // 1) Get (valid) token
            var token = await _tokens.GetRequiredAsync(businessId, ct).ConfigureAwait(false);

            // 2) Build URL
            var baseUrl = _opt.GraphBaseUrl.TrimEnd('/');
            var ver = _opt.GraphApiVersion.Trim('/');
            var url = $"{baseUrl}/{ver}/{path.TrimStart('/')}";

            var finalUrl = query is null ? url : QueryHelpers.AddQueryString(url, query);

            using var req = new HttpRequestMessage(HttpMethod.Get, finalUrl);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                FacebookErrorResponse? err = null;
                try { err = JsonSerializer.Deserialize<FacebookErrorResponse>(raw, JsonOpts); } catch { /* ignore */ }

                var msg = err?.Error?.Message ?? $"Graph GET {path} failed with HTTP {(int)res.StatusCode}";
                _log.LogWarning("Graph error: {Msg}. Raw: {Raw}", msg, Truncate(raw, 1000));

                throw new FacebookGraphException(
                    msg, err?.Error?.Type, err?.Error?.Code, err?.Error?.SubCode, err?.Error?.TraceId);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(raw, JsonOpts)
                       ?? throw new InvalidOperationException("Graph response was empty.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse Graph response for {Path}. Raw: {Raw}", path, Truncate(raw, 1200));
                throw;
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
