#nullable enable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FbContracts = xbytechat.api.Features.ESU.Facebook.Contracts;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Options;
using xbytechat.api.Features.ESU.Facebook.Contracts;

namespace xbytechat.api.Features.ESU.Facebook.Clients
{
    internal sealed class FacebookOauthClient : IFacebookOauthClient
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        { PropertyNameCaseInsensitive = true, WriteIndented = false };

        private readonly HttpClient _http;
        private readonly ILogger<FacebookOauthClient> _log;
        private readonly FacebookOauthOptions _opt;

        public FacebookOauthClient(HttpClient http, IOptions<FacebookOauthOptions> opt, ILogger<FacebookOauthClient> log)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _opt = opt?.Value ?? throw new ArgumentNullException(nameof(opt));
        }

        public async Task<FbContracts.FacebookTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Authorization code must be provided.", nameof(code));

            var baseUri = _opt.GraphBaseUrl.TrimEnd('/');
            var version = _opt.GraphApiVersion.Trim('/');
            var path = $"{baseUri}/{version}/oauth/access_token";

            var uri = new UriBuilder(path)
            {
                Query =
                    $"client_id={Uri.EscapeDataString(_opt.AppId)}" +
                    $"&redirect_uri={Uri.EscapeDataString(_opt.RedirectUri)}" +
                    $"&client_secret={Uri.EscapeDataString(_opt.AppSecret)}" +
                    $"&code={Uri.EscapeDataString(code)}"
            }.Uri;

            _log.LogInformation("Exchanging Facebook OAuth code for token via {Uri}", uri.GetLeftPart(UriPartial.Path));

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                FbContracts.FacebookErrorResponse? fbErr = null;
                try { fbErr = JsonSerializer.Deserialize<FbContracts.FacebookErrorResponse>(raw, JsonOpts); } catch { }

                var message = fbErr?.Error?.Message ?? $"Facebook token exchange failed with HTTP {(int)res.StatusCode}";
                _log.LogWarning("Facebook OAuth error: {Message}. Raw: {Raw}", message, Truncate(raw, 1000));
                throw new InvalidOperationException(
                    $"Facebook OAuth error: {message} (type={fbErr?.Error?.Type}, code={fbErr?.Error?.Code}, subcode={fbErr?.Error?.SubCode})");
            }

            FbContracts.FacebookTokenResponse? token;
            try { token = JsonSerializer.Deserialize<FbContracts.FacebookTokenResponse>(raw, JsonOpts); }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse Facebook token response. Raw: {Raw}", Truncate(raw, 1000));
                throw;
            }

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                _log.LogError("Facebook token response missing access_token. Raw: {Raw}", Truncate(raw, 1000));
                throw new InvalidOperationException("Facebook token response missing access_token.");
            }

            // attach raw JSON for auditing
            token = new FbContracts.FacebookTokenResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresInSeconds = token.ExpiresInSeconds,
                RawJson = raw
            };

            _log.LogInformation("Facebook OAuth token exchange succeeded. ExpiresIn(s)={Expires}", token.ExpiresInSeconds);
            return token;
        }
        public async Task<FbContracts.FacebookTokenResponse> ExchangeForLongLivedAsync(
      FbContracts.FacebookTokenResponse shortToken,
      CancellationToken ct = default)
        {
            if (shortToken is null || string.IsNullOrWhiteSpace(shortToken.AccessToken))
                throw new ArgumentException("Short-lived token is required.", nameof(shortToken));

            var baseUri = _opt.GraphBaseUrl.TrimEnd('/');
            var version = _opt.GraphApiVersion.Trim('/'); // e.g., v20.0
            var path = $"{baseUri}/{version}/oauth/access_token";

            var uri = new UriBuilder(path)
            {
                Query =
                    "grant_type=fb_exchange_token" +
                    $"&client_id={Uri.EscapeDataString(_opt.AppId)}" +
                    $"&client_secret={Uri.EscapeDataString(_opt.AppSecret)}" +
                    $"&fb_exchange_token={Uri.EscapeDataString(shortToken.AccessToken)}"
            }.Uri;

            _log.LogInformation("Exchanging short-lived token for long-lived via {Uri}", uri.GetLeftPart(UriPartial.Path));

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                FbContracts.FacebookErrorResponse? fbErr = null;
                try { fbErr = JsonSerializer.Deserialize<FbContracts.FacebookErrorResponse>(raw, JsonOpts); } catch { }

                var message = fbErr?.Error?.Message ?? $"Facebook long-lived exchange failed with HTTP {(int)res.StatusCode}";
                _log.LogWarning("Facebook OAuth long-lived error: {Message}. Raw: {Raw}", message, Truncate(raw, 1000));
                throw new InvalidOperationException(
                    $"Facebook OAuth error: {message} (type={fbErr?.Error?.Type}, code={fbErr?.Error?.Code}, subcode={fbErr?.Error?.SubCode})");
            }

            FbContracts.FacebookTokenResponse? token;
            try { token = JsonSerializer.Deserialize<FbContracts.FacebookTokenResponse>(raw, JsonOpts); }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse long-lived token response. Raw: {Raw}", Truncate(raw, 1000));
                throw;
            }

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                _log.LogError("Long-lived token response missing access_token. Raw: {Raw}", Truncate(raw, 1000));
                throw new InvalidOperationException("Invalid long-lived token response.");
            }

            return new FbContracts.FacebookTokenResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresInSeconds = token.ExpiresInSeconds, // usually ~5,184,000 (60 days)
                RawJson = raw
            };
        }


        private static string Truncate(string input, int max) => input.Length <= max ? input : input[..max];
    }
}
