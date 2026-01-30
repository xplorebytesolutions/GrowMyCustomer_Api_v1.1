using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Options;

namespace xbytechat.api.Features.ESU.Facebook.Clients
{
    internal sealed class WabaSubscriptionClient : IWabaSubscriptionClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly FacebookOptions _fb;
        private readonly ILogger<WabaSubscriptionClient> _log;

        public WabaSubscriptionClient(
            IHttpClientFactory httpFactory,
            IOptions<FacebookOptions> fb,
            ILogger<WabaSubscriptionClient> log)
        {
            _httpFactory = httpFactory;
            _fb = fb.Value;
            _log = log;
        }

        public async Task SubscribeAsync(string wabaId, string accessToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(wabaId)) throw new ArgumentException("wabaId is required", nameof(wabaId));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("accessToken is required", nameof(accessToken));

            var baseUrl = string.IsNullOrWhiteSpace(_fb.GraphBaseUrl) ? "https://graph.facebook.com" : _fb.GraphBaseUrl!;
            var version = string.IsNullOrWhiteSpace(_fb.GraphApiVersion) ? "v22.0" : _fb.GraphApiVersion!;
            var url = $"{baseUrl.TrimEnd('/')}/{version}/{wabaId}/subscribed_apps";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var http = _httpFactory.CreateClient();
            var res = await http.SendAsync(req, ct);

            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("⚠️ WABA subscribe failed. wabaId={WabaId}, status={Status}, body={Body}", wabaId, (int)res.StatusCode, body);
                res.EnsureSuccessStatusCode();
            }

            _log.LogInformation("✅ WABA subscribed. wabaId={WabaId}", wabaId);
        }

        public async Task UnsubscribeAsync(string wabaId, string accessToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(wabaId)) throw new ArgumentException("wabaId is required", nameof(wabaId));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("accessToken is required", nameof(accessToken));

            var baseUrl = string.IsNullOrWhiteSpace(_fb.GraphBaseUrl) ? "https://graph.facebook.com" : _fb.GraphBaseUrl!;
            var version = string.IsNullOrWhiteSpace(_fb.GraphApiVersion) ? "v22.0" : _fb.GraphApiVersion!;
            var url = $"{baseUrl.TrimEnd('/')}/{version}/{wabaId}/subscribed_apps";

            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var http = _httpFactory.CreateClient();
            var res = await http.SendAsync(req, ct);

            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("⚠️ WABA unsubscribe failed. wabaId={WabaId}, status={Status}, body={Body}", wabaId, (int)res.StatusCode, body);
                res.EnsureSuccessStatusCode();
            }

            _log.LogInformation("✅ WABA unsubscribed. wabaId={WabaId}", wabaId);
        }
    }
}
