using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.CustomeApi.Services
{
    public class CtaJourneyPublisher : ICtaJourneyPublisher
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<CtaJourneyPublisher> _log;

        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public CtaJourneyPublisher(
            AppDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<CtaJourneyPublisher> log)
        {
            _db = db;
            _httpFactory = httpFactory;
            _log = log;
        }

        public async Task PublishAsync(Guid businessId, Models.CtaJourneyEventDto dto, CancellationToken ct = default)
        {
            // Load all active endpoints (only for this one customer right now)
            var endpoints = await _db.CustomerWebhookConfigs
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive)
                .ToListAsync(ct);

            if (endpoints.Count == 0)
            {
                _log.LogInformation("CTA Journey: no active endpoints for business {Biz}", businessId);
                return;
            }

            var client = _httpFactory.CreateClient("customapi-webhooks"); // registered in DI

            foreach (var ep in endpoints)
            {
                // Serialize once per endpoint
                var body = JsonSerializer.Serialize(dto, _json);

                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url)
                        {
                            Content = new StringContent(body, Encoding.UTF8, "application/json")
                        };

                        if (!string.IsNullOrWhiteSpace(ep.BearerToken))
                            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.BearerToken);

                        var resp = await client.SendAsync(req, ct);
                        var code = (int)resp.StatusCode;

                        if (code >= 200 && code < 300)
                        {
                            _log.LogInformation("CTA Journey posted to {Url} | {Status}", ep.Url, code);
                            break; // success; stop retrying this endpoint
                        }

                        var errText = await resp.Content.ReadAsStringAsync(ct);
                        _log.LogWarning("CTA Journey post failed ({Code}) to {Url}: {Body}", code, ep.Url, errText);

                        if (attempt == maxAttempts) break;
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct); // 2s, 4s backoff
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "CTA Journey post exception to {Url} (attempt {Attempt})", ep.Url, attempt);
                        if (attempt == maxAttempts) break;
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                    }
                }
            }
        }

        public async Task<(bool ok, string message)> ValidateAndPingAsync(Guid businessId, CancellationToken ct = default)
        {
            var ep = await _db.CustomerWebhookConfigs
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (ep == null) return (false, "No active CustomerWebhookConfig found for this business.");
            if (string.IsNullOrWhiteSpace(ep.Url)) return (false, "Endpoint URL is empty.");
            if (!Uri.TryCreate(ep.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return (false, "Endpoint URL must be an absolute https URL.");

            var probe = new Models.CtaJourneyEventDto
            {
                userId = null,
                userName = "probe",
                userPhone = "0000000000",
                botId = "0000000000",
                categoryBrowsed = null,
                productBrowsed = null,
                CTAJourney = "probe_to_probe"
            };

            var client = _httpFactory.CreateClient("customapi-webhooks");
            var body = JsonSerializer.Serialize(probe, _json);

            using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            req.Headers.TryAddWithoutValidation("X-XBS-Test", "1");

            if (!string.IsNullOrWhiteSpace(ep.BearerToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.BearerToken);

            try
            {
                var resp = await client.SendAsync(req, ct);
                var code = (int)resp.StatusCode;

                if (code >= 200 && code < 300) return (true, $"OK ({code})");

                var text = await resp.Content.ReadAsStringAsync(ct);
                return (false, $"HTTP {code}: {text}");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
    }
}


//using System;
//using System.Linq;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;

//namespace xbytechat.api.Features.CustomeApi.Services
//{
//    public class CtaJourneyPublisher : ICtaJourneyPublisher
//    {
//        private readonly AppDbContext _db;
//        private readonly IHttpClientFactory _httpFactory;
//        private readonly ILogger<CtaJourneyPublisher> _log;

//        private static readonly JsonSerializerOptions _json =
//            new(JsonSerializerDefaults.Web);

//        public CtaJourneyPublisher(AppDbContext db, IHttpClientFactory httpFactory, ILogger<CtaJourneyPublisher> log)
//        {
//            _db = db;
//            _httpFactory = httpFactory;
//            _log = log;
//        }


//        public async Task PublishAsync(Guid businessId, Models.CtaJourneyEventDto dto, CancellationToken ct = default)
//        {
//            // load all active endpoints (only for this one customer right now)
//            var endpoints = await _db.CustomerWebhookConfigs
//                .AsNoTracking()
//                .Where(x => x.BusinessId == businessId && x.IsActive)
//                .ToListAsync(ct);

//            if (endpoints.Count == 0)
//            {
//                _log.LogInformation("CTA Journey: no active endpoints for business {Biz}", businessId);
//                return;
//            }

//            var client = _httpFactory.CreateClient("customapi-webhooks"); // registered in DI
//            var body = JsonSerializer.Serialize(dto, _json);
//            using var content = new StringContent(body, Encoding.UTF8, "application/json");
//            foreach (var ep in endpoints)
//            {
//                // simple retry (3 attempts, 2s/4s backoff)
//                const int maxAttempts = 3;
//                for (int attempt = 1; attempt <= maxAttempts; attempt++)
//                {
//                    try
//                    {
//                        using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url)
//                        {
//                            Content = new StringContent(
//                                JsonSerializer.Serialize(dto, _json),   // fresh content every send
//                                Encoding.UTF8,
//                                "application/json")
//                        };

//                        if (!string.IsNullOrWhiteSpace(ep.BearerToken))
//                            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.BearerToken);

//                        var resp = await client.SendAsync(req, ct);
//                        if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
//                        {
//                            _log.LogInformation("CTA Journey posted to {Url} | {Status}", ep.Url, (int)resp.StatusCode);
//                            break;
//                        }

//                        var bodyText = await resp.Content.ReadAsStringAsync(ct);
//                        _log.LogWarning("CTA Journey post failed ({Code}) to {Url}: {Body}",
//                            (int)resp.StatusCode, ep.Url, bodyText);

//                        if (attempt == maxAttempts) break;
//                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
//                    }
//                    catch (Exception ex)
//                    {
//                        _log.LogWarning(ex, "CTA Journey post exception to {Url} (attempt {Attempt})", ep.Url, attempt);
//                        if (attempt == maxAttempts) break;
//                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
//                    }
//                }
//            }

//            //foreach (var ep in endpoints)
//            //{
//            //    using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url) { Content = content };

//            //    // optional Bearer only (we're keeping it simple)
//            //    if (!string.IsNullOrWhiteSpace(ep.BearerToken))
//            //        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.BearerToken);

//            //    // simple retry (3 attempts, 2s/4s backoff)
//            //    const int maxAttempts = 3;
//            //    for (int attempt = 1; attempt <= maxAttempts; attempt++)
//            //    {
//            //        try
//            //        {
//            //            var resp = await client.SendAsync(req, ct);
//            //            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
//            //            {
//            //                _log.LogInformation("CTA Journey posted to {Url} | {Status}", ep.Url, (int)resp.StatusCode);
//            //                break;
//            //            }

//            //            var bodyText = await resp.Content.ReadAsStringAsync(ct);
//            //            _log.LogWarning("CTA Journey post failed ({Code}) to {Url}: {Body}",
//            //                (int)resp.StatusCode, ep.Url, bodyText);

//            //            if (attempt == maxAttempts) break;
//            //            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
//            //        }
//            //        catch (Exception ex)
//            //        {
//            //            _log.LogWarning(ex, "CTA Journey post exception to {Url} (attempt {Attempt})", ep.Url, attempt);
//            //            if (attempt == maxAttempts) break;
//            //            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
//            //        }
//            //    }
//            //}
//        }
//        public async Task<(bool ok, string message)> ValidateAndPingAsync(Guid businessId, CancellationToken ct = default)
//        {
//            var ep = await _db.CustomerWebhookConfigs
//                .AsNoTracking()
//                .Where(x => x.BusinessId == businessId && x.IsActive)
//                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
//                .FirstOrDefaultAsync(ct);

//            if (ep == null) return (false, "No active CustomerWebhookConfig found for this business.");
//            if (string.IsNullOrWhiteSpace(ep.Url)) return (false, "Endpoint URL is empty.");
//            if (!Uri.TryCreate(ep.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
//                return (false, "Endpoint URL must be an absolute https URL.");

//            var probe = new Models.CtaJourneyEventDto
//            {
//                userId = null,
//                userName = "probe",
//                userPhone = "0000000000",
//                botId = "0000000000",
//                categoryBrowsed = null,
//                productBrowsed = null,
//                CTAJourney = "probe_to_probe"
//            };

//            var client = _httpFactory.CreateClient("customapi-webhooks");
//            var body = JsonSerializer.Serialize(probe, _json);

//            using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url)
//            {
//                Content = new StringContent(body, Encoding.UTF8, "application/json")
//            };
//            req.Headers.TryAddWithoutValidation("X-XBS-Test", "1");

//            if (!string.IsNullOrWhiteSpace(ep.BearerToken))
//                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.BearerToken);

//            try
//            {
//                var resp = await client.SendAsync(req, ct);
//                var code = (int)resp.StatusCode;
//                if (code >= 200 && code < 300) return (true, $"OK ({code})");
//                var text = await resp.Content.ReadAsStringAsync(ct);
//                return (false, $"HTTP {code}: {text}");
//            }
//            catch (Exception ex)
//            {
//                return (false, $"Exception: {ex.Message}");
//            }
//        }

//    }
//}

