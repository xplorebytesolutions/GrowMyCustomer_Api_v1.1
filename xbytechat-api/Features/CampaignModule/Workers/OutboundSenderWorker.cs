// 📄 File: Features/CampaignModule/Workers/OutboundSenderWorker.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

using xbytechat.api;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignTracking.Logging;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.MessageLogging.Services;
using xbytechat.api.Features.MessagesEngine.Abstractions;
using xbytechat.api.Features.CampaignModule.SendEngine;
using xbytechat_api.Features.Billing.Services;
using xbytechat.api.Infrastructure.Observability;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.WhatsAppSettings.Helpers;
using xbytechat.api.AuthModule.Models;

namespace xbytechat.api.Features.CampaignModule.Workers
{
    public class OutboundSenderWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<OutboundSenderWorker> _log;
        private readonly Channel<OutboundMessageJob> _channel;
        private const int DEFAULT_MAX_ATTEMPTS = 3;

        // Concurrency caps
        private readonly int _globalDop = 32;   // total parallel consumers
        private readonly int _perNumberDop = 8; // per (provider, PhoneNumberId)

        private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan _flightTimeout = TimeSpan.FromMinutes(5);

        private int _inChannel;
        private readonly Random _rand = new();

        // =========================[ Template Button Cache + Parsers ]=========================
        private static readonly ConcurrentDictionary<string, IReadOnlyList<ButtonMeta>> _btnCache =
            new(StringComparer.Ordinal);

        private static readonly Regex RxButtonTypeMeta = new(@"^\s*url\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxButtonTypePinn = new(@"^\s*url\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string CacheKeyForTemplate(WhatsAppTemplate tpl)
            => $"{tpl.BusinessId}|{tpl.Provider}|{tpl.Name}|{tpl.LanguageCode}".ToUpperInvariant();

        private static IReadOnlyList<ButtonMeta> GetTemplateButtonsCached(WhatsAppTemplate tpl)
            => _btnCache.GetOrAdd(CacheKeyForTemplate(tpl), _ => ParseButtonsFromTemplateRow(tpl));

        private static IReadOnlyList<ButtonMeta> ParseButtonsFromTemplateRow(WhatsAppTemplate tpl)
        {
            if (!string.IsNullOrWhiteSpace(tpl.UrlButtons))
            {
                try
                {
                    using var doc = JsonDocument.Parse(tpl.UrlButtons);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<ButtonMeta>(capacity: 3);
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var text = el.TryGetProperty("ButtonText", out var pText) ? pText.GetString() ?? "" :
                                       el.TryGetProperty("Text", out var pText2) ? pText2.GetString() ?? "" : "";
                            var type = el.TryGetProperty("ButtonType", out var pType) ? pType.GetString() ?? "" :
                                       el.TryGetProperty("Type", out var pType2) ? pType2.GetString() ?? "" : "";
                            var url = el.TryGetProperty("TargetUrl", out var pUrl) ? pUrl.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(type))
                                list.Add(new ButtonMeta(text, type, url));
                        }
                        return list;
                    }
                }
                catch { /* fallthrough */ }
            }

            if (!string.IsNullOrWhiteSpace(tpl.RawJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(tpl.RawJson);

                    JsonElement comps = default;
                    if (doc.RootElement.TryGetProperty("template", out var tplNode) &&
                        tplNode.TryGetProperty("components", out var comps1))
                    {
                        comps = comps1;
                    }
                    else if (doc.RootElement.TryGetProperty("components", out var comps2))
                    {
                        comps = comps2;
                    }

                    if (comps.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<ButtonMeta>(capacity: 3);

                        foreach (var c in comps.EnumerateArray())
                        {
                            if (!c.TryGetProperty("type", out var tProp)) continue;
                            if (!string.Equals(tProp.GetString(), "button", StringComparison.OrdinalIgnoreCase)) continue;

                            var isUrl =
                                (c.TryGetProperty("sub_type", out var st) && RxButtonTypeMeta.IsMatch(st.GetString() ?? "")) ||
                                (c.TryGetProperty("subType", out var st2) && RxButtonTypePinn.IsMatch(st2.GetString() ?? ""));

                            if (!isUrl) continue;

                            string text = "Open";
                            string type = "url";
                            string? url = null;

                            if (c.TryGetProperty("parameters", out var pars) && pars.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var p in pars.EnumerateArray())
                                {
                                    if (p.TryGetProperty("text", out var txtProp))
                                    {
                                        var v = txtProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(v)) { url = v; break; }
                                    }
                                }
                            }

                            list.Add(new ButtonMeta(text, type, url));
                            if (list.Count >= 3) break;
                        }

                        return list;
                    }
                }
                catch { /* ignore */ }
            }

            return Array.Empty<ButtonMeta>();
        }
        // =====================================================================================

        public OutboundSenderWorker(IServiceProvider sp, ILogger<OutboundSenderWorker> log)
        {
            _sp = sp;
            _log = log;

            _channel = Channel.CreateBounded<OutboundMessageJob>(new BoundedChannelOptions(5000)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = cts.Token;

            var consumers = Enumerable.Range(0, _globalDop)
                .Select(_ => Task.Run(() => ConsumeAsync(ct), ct))
                .ToArray();

            var producer = Task.Run(() => ProduceAsync(ct), ct);

            var all = consumers.Append(producer).ToArray();

            try
            {
                await Task.WhenAll(all);
            }
            catch
            {
                try { cts.Cancel(); } catch { /* ignore */ }
                throw;
            }
        }

        private async Task ProduceAsync(CancellationToken ct)
        {
            const int ChannelCapacity = 5000;

            var idleDelay = _pollInterval;
            var maxIdleDelay = TimeSpan.FromSeconds(1);
            var longIdleDelay = TimeSpan.FromSeconds(30);
            int consecutiveEmpty = 0;

            const string sql = @"
WITH cte AS (
    SELECT ""Id""
    FROM ""OutboundMessageJobs""
    WHERE ""Status"" = 'Pending'
      AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= NOW())
    ORDER BY ""NextAttemptAt"" NULLS FIRST, ""CreatedAt""
    FOR UPDATE SKIP LOCKED
    LIMIT @take
)
UPDATE ""OutboundMessageJobs"" j
SET ""Status"" = 'InFlight',
    ""NextAttemptAt"" = NOW() + make_interval(secs => @flight),
    ""LastError"" = NULL
WHERE j.""Id"" IN (SELECT ""Id"" FROM cte)
RETURNING j.*;";

            try { await Task.Delay(_rand.Next(100, 500), ct); } catch { /* ignore */ }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var approxCount = Volatile.Read(ref _inChannel);
                    var budget = Math.Max(0, ChannelCapacity - approxCount);

                    if (budget <= 0)
                    {
                        await Task.Delay(idleDelay, ct);
                        idleDelay = TimeSpan.FromMilliseconds(
                            Math.Min(maxIdleDelay.TotalMilliseconds, idleDelay.TotalMilliseconds * 1.25)
                        );
                        continue;
                    }

                    idleDelay = _pollInterval;

                    var take = Math.Min(budget, 2000);
                    var flightSecs = (int)Math.Ceiling(_flightTimeout.TotalSeconds);

                    var takeParam = new NpgsqlParameter<int>("take", take);
                    var flightParam = new NpgsqlParameter<int>("flight", flightSecs);

                    var prevTimeout = db.Database.GetCommandTimeout();
                    db.Database.SetCommandTimeout(5);

                    List<OutboundMessageJob> jobs;
                    try
                    {
                        jobs = await db.OutboundMessageJobs
                            .FromSqlRaw(sql, takeParam, flightParam)
                            .AsNoTracking()
                            .ToListAsync(ct);
                    }
                    finally
                    {
                        db.Database.SetCommandTimeout(prevTimeout);
                    }

                    foreach (var job in jobs)
                    {
                        await _channel.Writer.WriteAsync(job, ct);
                        Interlocked.Increment(ref _inChannel);
                    }

                    if (jobs.Count == 0)
                    {
                        consecutiveEmpty++;
                        var jitterMs = _rand.Next(0, 200);
                        var delay = idleDelay + TimeSpan.FromMilliseconds(jitterMs);

                        if (consecutiveEmpty >= 8 && idleDelay >= maxIdleDelay)
                            delay = longIdleDelay + TimeSpan.FromMilliseconds(jitterMs);

                        await Task.Delay(delay, ct);

                        if (idleDelay < maxIdleDelay)
                        {
                            idleDelay = TimeSpan.FromMilliseconds(
                                Math.Min(maxIdleDelay.TotalMilliseconds, idleDelay.TotalMilliseconds * 2)
                            );
                        }
                    }
                    else
                    {
                        consecutiveEmpty = 0;
                        idleDelay = _pollInterval;
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[Outbox] Producer loop error");
                    try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch { /* ignore */ }
                }
            }
        }

        // ---- keyed concurrency gate (per sender) ---------------------------------------
        private sealed class KeyedSemaphore
        {
            private readonly ConcurrentDictionary<string, SemaphoreSlim> _map = new();
            public async Task<IDisposable> AcquireAsync(string key, int dop, CancellationToken ct)
            {
                var sem = _map.GetOrAdd(key, _ => new SemaphoreSlim(dop));
                await sem.WaitAsync(ct);
                return new Releaser(sem);
            }
            private sealed class Releaser : IDisposable
            {
                private readonly SemaphoreSlim _s;
                public Releaser(SemaphoreSlim s) => _s = s;
                public void Dispose() => _s.Release();
            }
        }
        private static readonly KeyedSemaphore _perSenderGate = new();
        // -------------------------------------------------------------------------------

        // Best-effort extraction of provider message id from a Meta success body
        private static string? TryExtractProviderMessageId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("messages", out var msgs) &&
                    msgs.ValueKind == JsonValueKind.Array &&
                    msgs.GetArrayLength() > 0 &&
                    msgs[0].TryGetProperty("id", out var idProp))
                {
                    return idProp.GetString();
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private async Task ConsumeAsync(CancellationToken ct)
        {
            static async Task<string> ResolveRecipientPhoneAsync(AppDbContext db, Guid recipientId, CancellationToken ct2)
            {
                var phone = await db.CampaignRecipients
                    .AsNoTracking()
                    .Where(r => r.Id == recipientId)
                    .Select(r =>
                        r.Contact != null
                            ? r.Contact.PhoneNumber
                            : (r.AudienceMember != null
                                ? (r.AudienceMember.PhoneE164 ?? r.AudienceMember.PhoneRaw)
                                : null))
                    .FirstOrDefaultAsync(ct2);

                return phone ?? string.Empty;
            }

            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                if (!_channel.Reader.TryRead(out var job))
                    continue;

                Interlocked.Decrement(ref _inChannel);

                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var engine = scope.ServiceProvider.GetRequiredService<IMessageEngineService>();
                    var billing = scope.ServiceProvider.GetRequiredService<IBillingIngestService>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<OutboundSenderWorker>>();
                    var logSink = scope.ServiceProvider.GetRequiredService<ICampaignLogSink>();
                    var limiter = scope.ServiceProvider.GetRequiredService<xbytechat.api.Infrastructure.RateLimiting.IPhoneNumberRateLimiter>();
                    var messageLogSink = scope.ServiceProvider.GetRequiredService<IMessageLogSink>();
                    var builder = scope.ServiceProvider.GetRequiredService<ITemplatePayloadBuilder>();
                    var validator = scope.ServiceProvider.GetRequiredService<ICampaignSendValidator>();

                    var senderKey = $"{job.Provider}|{job.PhoneNumberId}";

                    using (await _perSenderGate.AcquireAsync(senderKey, _perNumberDop, ct))
                    {
                        var lease = await limiter.AcquireAsync(senderKey, ct);
                        if (!lease.IsAcquired)
                        {
                            await Task.Delay(50, ct);
                            continue;
                        }

                        var recipientPhone = await ResolveRecipientPhoneAsync(db, job.RecipientId, ct);
                        if (string.IsNullOrWhiteSpace(recipientPhone))
                        {
                            job.Status = "Failed";
                            job.Attempt += 1;
                            job.LastError = "Recipient phone not found.";
                            var backoff1 = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff1);
                            db.Update(job);
                            await db.SaveChangesAsync(ct);

                            logger.LogWarning("[Outbox] Recipient phone not found. job={JobId} recipient={RecipientId}", job.Id, job.RecipientId);
                            MetricsRegistry.MessagesFailed.Add(1);
                            continue;
                        }

                        var lang = job.LanguageCode ?? "en_US";
                        var tmplRow = await db.WhatsAppTemplates
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t =>
                                t.BusinessId == job.BusinessId &&
                                t.Provider == job.Provider &&
                                t.Name == job.TemplateName &&
                                t.LanguageCode == lang,
                                ct);

                        if (tmplRow == null)
                        {
                            job.Status = "Failed";
                            job.Attempt += 1;
                            job.LastError = $"Template not found: {job.TemplateName} ({lang}) for {job.Provider}.";
                            var backoff = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
                            db.Update(job);
                            await db.SaveChangesAsync(ct);

                            logger.LogWarning("[Outbox] Template not found. job={JobId} template={Template} lang={Lang} provider={Provider}",
                                job.Id, job.TemplateName, lang, job.Provider);
                            MetricsRegistry.MessagesFailed.Add(1);
                            continue;
                        }

                        HeaderKind headerKind = tmplRow.HeaderKind?.ToLowerInvariant() switch
                        {
                            "text" => HeaderKind.Text,
                            "image" => HeaderKind.Image,
                            "video" => HeaderKind.Video,
                            "document" => HeaderKind.Document,
                            "none" or null => HeaderKind.None,
                            _ => HeaderKind.None
                        };
                        if (headerKind == HeaderKind.None && !string.IsNullOrWhiteSpace(job.MediaType))
                        {
                            headerKind = job.MediaType.ToLowerInvariant() switch
                            {
                                "text" => HeaderKind.Text,
                                "image" => HeaderKind.Image,
                                "video" => HeaderKind.Video,
                                "document" => HeaderKind.Document,
                                _ => HeaderKind.None
                            };
                        }

                        var providerEnum = ProviderUtil.Parse(job.Provider);
                        var buttonsFromTemplate = GetTemplateButtonsCached(tmplRow);

                        var plan = new SendPlan(
                            BusinessId: job.BusinessId,
                            Provider: providerEnum,
                            PhoneNumberId: job.PhoneNumberId!,
                            TemplateName: job.TemplateName!,
                            LanguageCode: lang,
                            HeaderKind: headerKind,
                            HeaderUrl: job.HeaderMediaUrl,
                            Buttons: buttonsFromTemplate
                        );

                        var recipient = new RecipientPlan(
                            RecipientId: job.RecipientId,
                            ToPhoneE164: recipientPhone,
                            ParametersJson: job.ResolvedParamsJson ?? "[]",
                            ButtonParamsJson: job.ResolvedButtonUrlsJson,
                            IdempotencyKey: job.IdempotencyKey ?? $"{job.CampaignId}:{recipientPhone}:{job.TemplateName}"
                        );

                        var envelope = builder.Build(plan, recipient);

                        var (ok, error) = validator.Validate(plan, recipient, envelope, tmplRow);
                        if (!ok)
                        {
                            job.Status = "Failed";
                            job.Attempt += 1;
                            job.LastError = error ?? "Validation failed.";
                            var backoff = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
                            db.Update(job);
                            await db.SaveChangesAsync(ct);

                            logger.LogWarning("[Outbox] Validation failed job={JobId} error={Error}", job.Id, job.LastError);
                            MetricsRegistry.MessagesFailed.Add(1);
                            continue;
                        }

                        object payload = providerEnum switch
                        {
                            Provider.MetaCloud =>
                                scope.ServiceProvider.GetRequiredService<MetaCloudPayloadMapper>()
                                    .BuildPayload(plan, recipient, envelope),

                            Provider.Pinnacle =>
                                scope.ServiceProvider.GetRequiredService<PinnaclePayloadMapper>()
                                    .BuildPayload(plan, recipient, envelope),

                            _ => throw new InvalidOperationException("Unknown provider")
                        };

                        var sw = Stopwatch.StartNew();
                        var engineResult = await engine.SendPayloadAsync(job.BusinessId, job.Provider, payload, job.PhoneNumberId);
                        sw.Stop();
                        MetricsRegistry.SendLatencyMs.Record(sw.Elapsed.TotalMilliseconds);

                        if (!engineResult.Success)
                        {
                            var err = engineResult.ErrorMessage ?? string.Empty;
                            if (err.Contains("429", StringComparison.Ordinal) ||
                                err.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
                            {
                                limiter.UpdateLimits(senderKey, permitsPerSecond: 5, burst: 5);
                                MetricsRegistry.RateLimited429s.Add(1);
                            }
                        }

                        // ✅ Surface provider ID and raw body for visibility
                        var providerMsgId = !string.IsNullOrWhiteSpace(engineResult.MessageId)
                            ? engineResult.MessageId
                            : TryExtractProviderMessageId(engineResult.RawResponse);

                        if (engineResult.Success)
                        {
                            logger.LogInformation("Provider send OK | providerMessageId={ProviderId} job={JobId} to={To}",
                                providerMsgId, job.Id, recipientPhone);
                        }
                        else
                        {
                            logger.LogWarning("Provider send FAILED | job={JobId} to={To} error={Error} body={Body}",
                                job.Id, recipientPhone, engineResult.ErrorMessage, engineResult.RawResponse);
                        }

                        // Persist MessageLogs (COPY sink)
                        var now = DateTime.UtcNow;
                        var runId = Guid.NewGuid();
                        var logId = Guid.NewGuid();

                        logger.LogInformation(
                            "[OutboundSenderWorker] Enqueue MessageLog id={LogId} recipient={RecipientId} job={JobId}",
                            logId, job.RecipientId, job.Id);

                        messageLogSink.Enqueue(new MessageLog
                        {
                            Id = logId,
                            BusinessId = job.BusinessId,
                            CampaignId = job.CampaignId,
                            RecipientNumber = recipientPhone,
                            MessageContent = job.TemplateName,
                            MediaUrl = job.HeaderMediaUrl,
                            Status = engineResult.Success ? "Sent" : "Failed",
                            MessageId = providerMsgId,
                            ErrorMessage = engineResult.ErrorMessage,
                            RawResponse = engineResult.RawResponse,
                            CreatedAt = now,
                            SentAt = engineResult.Success ? now : (DateTime?)null,
                            Source = "campaign",
                            RunId = runId,
                            Provider = job.Provider,
                            ProviderMessageId = providerMsgId,
                            IsIncoming = false,
                            IsChargeable = false
                        });

                        // Batched CampaignSendLog (COPY via sink)
                        logSink.Enqueue(new CampaignLogRecord(
                            Id: Guid.NewGuid(),
                            RunId: runId,
                            MessageId: providerMsgId,
                            CampaignId: job.CampaignId,
                            ContactId: null,
                            RecipientId: job.RecipientId,
                            MessageBody: job.MessageBody ?? job.TemplateName,
                            TemplateId: job.TemplateName,
                            SendStatus: engineResult.Success ? "Sent" : "Failed",
                            ErrorMessage: engineResult.ErrorMessage,
                            CreatedAt: now,
                            CreatedBy: "system",
                            SentAt: engineResult.Success ? now : (DateTime?)null,
                            DeliveredAt: null,
                            ReadAt: null,
                            IpAddress: null,
                            DeviceInfo: null,
                            MacAddress: null,
                            SourceChannel: "campaign",
                            DeviceType: null,
                            Browser: null,
                            Country: null,
                            City: null,
                            IsClicked: false,
                            ClickedAt: null,
                            ClickType: null,
                            RetryCount: job.Attempt,
                            LastRetryAt: now,
                            LastRetryStatus: engineResult.Success ? "Success" : "Failed",
                            AllowRetry: job.Attempt < DEFAULT_MAX_ATTEMPTS,
                            MessageLogId: logId,
                            BusinessId: job.BusinessId,
                            CTAFlowConfigId: null,
                            CTAFlowStepId: null,
                            ButtonBundleJson: null
                        ));

                        // Update job (retry/backoff if needed)
                        job.Status = engineResult.Success ? "Sent" : "Failed";
                        job.Attempt += 1;
                        job.LastError = engineResult.ErrorMessage;

                        if (!engineResult.Success)
                        {
                            var backoffSecs = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSecs);
                            MetricsRegistry.MessagesFailed.Add(1);
                        }
                        else
                        {
                            job.NextAttemptAt = null;
                            MetricsRegistry.MessagesSent.Add(1);
                        }

                        db.Update(job);
                        await db.SaveChangesAsync(ct);

                        await billing.IngestFromSendResponseAsync(
                            job.BusinessId,
                            logId,
                            job.Provider,
                            engineResult.RawResponse ?? "{}"
                        );
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    try
                    {
                        using var scope = _sp.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        job.Status = "Failed";
                        job.Attempt += 1;
                        job.LastError = ex.Message;
                        var backoffSecs = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
                        job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSecs);
                        db.Update(job);
                        await db.SaveChangesAsync(ct);
                    }
                    catch { /* swallow */ }

                    MetricsRegistry.MessagesFailed.Add(1);
                    _log.LogError(ex, "[Outbox] Consume error job={JobId}", job.Id);
                }
            }
        }
    }
}


//// 📄 File: Features/CampaignModule/Workers/OutboundSenderWorker.cs
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;   // NEW
//using System.Diagnostics;
//using System.Linq;
//using System.Text.Json;            // NEW
//using System.Text.RegularExpressions; // NEW
//using System.Threading;
//using System.Threading.Channels;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Npgsql;

//using xbytechat.api;
//using xbytechat.api.Features.CampaignModule.Models;
//using xbytechat.api.Features.CampaignTracking.Logging;
//using xbytechat.api.Features.CampaignTracking.Models;
//using xbytechat.api.Features.MessageLogging.Services;
//using xbytechat.api.Features.MessagesEngine.Abstractions;
//using xbytechat.api.Features.CampaignModule.SendEngine;
//using xbytechat_api.Features.Billing.Services;
//using xbytechat.api.Infrastructure.Observability;
//using xbytechat.api.Features.MessagesEngine.Services;
//using xbytechat.api.WhatsAppSettings.Helpers;
//using xbytechat.api.AuthModule.Models;

//namespace xbytechat.api.Features.CampaignModule.Workers
//{
//    public class OutboundSenderWorker : BackgroundService
//    {
//        private readonly IServiceProvider _sp;
//        private readonly ILogger<OutboundSenderWorker> _log;
//        private readonly Channel<OutboundMessageJob> _channel;
//        private const int DEFAULT_MAX_ATTEMPTS = 3;

//        // Concurrency caps (override via appsettings if you expose options)
//        private readonly int _globalDop = 32;   // total parallel consumers
//        private readonly int _perNumberDop = 8; // per (provider, PhoneNumberId)

//        private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
//        private static readonly TimeSpan _flightTimeout = TimeSpan.FromMinutes(5);

//        private int _inChannel;
//        private readonly Random _rand = new();

//        // =========================[ NEW: Template Button Cache + Parsers ]=========================
//        // Cache key = BusinessId|Provider|TemplateName|Language (upper-invariant)
//        private static readonly ConcurrentDictionary<string, IReadOnlyList<ButtonMeta>> _btnCache =
//            new(StringComparer.Ordinal);

//        // Precompiled regexes for any legacy/raw variations (cheap + fast on hot path)
//        private static readonly Regex RxButtonTypeMeta = new(@"^\s*url\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
//        private static readonly Regex RxButtonTypePinn = new(@"^\s*url\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

//        private static string CacheKeyForTemplate(WhatsAppTemplate tpl)
//            => $"{tpl.BusinessId}|{tpl.Provider}|{tpl.Name}|{tpl.LanguageCode}".ToUpperInvariant();

//        private static IReadOnlyList<ButtonMeta> GetTemplateButtonsCached(WhatsAppTemplate tpl)
//            => _btnCache.GetOrAdd(CacheKeyForTemplate(tpl), _ => ParseButtonsFromTemplateRow(tpl));

//        private static IReadOnlyList<ButtonMeta> ParseButtonsFromTemplateRow(WhatsAppTemplate tpl)
//        {
//            // 1) Preferred: light-weight ButtonsJson (DTO-style) if available
//            if (!string.IsNullOrWhiteSpace(tpl.UrlButtons))
//            {
//                try
//                {
//                    using var doc = JsonDocument.Parse(tpl.UrlButtons);
//                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
//                    {
//                        var list = new List<ButtonMeta>(capacity: 3);
//                        foreach (var el in doc.RootElement.EnumerateArray())
//                        {
//                            // Support both historical shapes:
//                            // { "ButtonText","ButtonType","TargetUrl" }  OR  { "Text","Type","TargetUrl" }
//                            var text = el.TryGetProperty("ButtonText", out var pText) ? pText.GetString() ?? "" :
//                                       el.TryGetProperty("Text", out var pText2) ? pText2.GetString() ?? "" : "";
//                            var type = el.TryGetProperty("ButtonType", out var pType) ? pType.GetString() ?? "" :
//                                       el.TryGetProperty("Type", out var pType2) ? pType2.GetString() ?? "" : "";
//                            var url = el.TryGetProperty("TargetUrl", out var pUrl) ? pUrl.GetString() : null;

//                            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(type))
//                                list.Add(new ButtonMeta(text, type, url));
//                        }
//                        return list;
//                    }
//                }
//                catch
//                {
//                    // Fallthrough to RawJson parser
//                }
//            }

//            // 2) Fallback: parse RawJson (provider-native). Look for button components with url subtype.
//            if (!string.IsNullOrWhiteSpace(tpl.RawJson))
//            {
//                try
//                {
//                    using var doc = JsonDocument.Parse(tpl.RawJson);

//                    // Try to locate "components" either under "template" or at root
//                    JsonElement comps = default;
//                    if (doc.RootElement.TryGetProperty("template", out var tplNode) &&
//                        tplNode.TryGetProperty("components", out var comps1))
//                    {
//                        comps = comps1;
//                    }
//                    else if (doc.RootElement.TryGetProperty("components", out var comps2))
//                    {
//                        comps = comps2;
//                    }

//                    if (comps.ValueKind == JsonValueKind.Array)
//                    {
//                        var list = new List<ButtonMeta>(capacity: 3);

//                        foreach (var c in comps.EnumerateArray())
//                        {
//                            // Meta:   { "type":"button", "sub_type":"url", "index":"0", ... }
//                            // Pinn.:  { "type":"button", "subType":"url", "index": 0, ... }
//                            if (!c.TryGetProperty("type", out var tProp)) continue;
//                            if (!string.Equals(tProp.GetString(), "button", StringComparison.OrdinalIgnoreCase)) continue;

//                            var isUrl =
//                                (c.TryGetProperty("sub_type", out var st) && RxButtonTypeMeta.IsMatch(st.GetString() ?? "")) ||
//                                (c.TryGetProperty("subType", out var st2) && RxButtonTypePinn.IsMatch(st2.GetString() ?? ""));

//                            if (!isUrl) continue;

//                            // Text label may not be present in raw; use a neutral label
//                            string text = "Open";
//                            string type = "url";
//                            string? url = null;

//                            // If static url parameter is present, try to read it
//                            if (c.TryGetProperty("parameters", out var pars) && pars.ValueKind == JsonValueKind.Array)
//                            {
//                                foreach (var p in pars.EnumerateArray())
//                                {
//                                    if (p.TryGetProperty("text", out var txtProp))
//                                    {
//                                        var v = txtProp.GetString();
//                                        if (!string.IsNullOrWhiteSpace(v)) { url = v; break; }
//                                    }
//                                }
//                            }

//                            list.Add(new ButtonMeta(text, type, url));
//                            if (list.Count >= 3) break; // WhatsApp UI supports up to 3 buttons
//                        }

//                        return list;
//                    }
//                }
//                catch
//                {
//                    // ignore, fallthrough
//                }
//            }

//            return Array.Empty<ButtonMeta>();
//        }
//        // ==========================================================================================

//        public OutboundSenderWorker(IServiceProvider sp, ILogger<OutboundSenderWorker> log)
//        {
//            _sp = sp;
//            _log = log;

//            _channel = Channel.CreateBounded<OutboundMessageJob>(new BoundedChannelOptions(5000)
//            {
//                SingleReader = false,
//                SingleWriter = false,
//                FullMode = BoundedChannelFullMode.Wait
//            });
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
//            var ct = cts.Token;

//            var consumers = Enumerable.Range(0, _globalDop)
//                .Select(_ => Task.Run(() => ConsumeAsync(ct), ct))
//                .ToArray();

//            var producer = Task.Run(() => ProduceAsync(ct), ct);

//            var all = consumers.Append(producer).ToArray();

//            try
//            {
//                await Task.WhenAll(all);
//            }
//            catch
//            {
//                try { cts.Cancel(); } catch { /* ignore */ }
//                throw;
//            }
//        }

//        private async Task ProduceAsync(CancellationToken ct)
//        {
//            const int ChannelCapacity = 5000;

//            // Backoff controls
//            var idleDelay = _pollInterval;                 // starts small
//            var maxIdleDelay = TimeSpan.FromSeconds(1);    // back off up to 1s
//            var longIdleDelay = TimeSpan.FromSeconds(30);  // after many empties
//            int consecutiveEmpty = 0;

//            // Prioritize due items; then FIFO by CreatedAt
//            const string sql = @"
//WITH cte AS (
//    SELECT ""Id""
//    FROM ""OutboundMessageJobs""
//    WHERE ""Status"" = 'Pending'
//      AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= NOW())
//    ORDER BY ""NextAttemptAt"" NULLS FIRST, ""CreatedAt""
//    FOR UPDATE SKIP LOCKED
//    LIMIT @take
//)
//UPDATE ""OutboundMessageJobs"" j
//SET ""Status"" = 'InFlight',
//    ""NextAttemptAt"" = NOW() + make_interval(secs => @flight),
//    ""LastError"" = NULL
//WHERE j.""Id"" IN (SELECT ""Id"" FROM cte)
//RETURNING j.*;";

//            // Small jittered pause on startup to avoid log bursts on app boot
//            try { await Task.Delay(_rand.Next(100, 500), ct); } catch { /* ignore */ }

//            while (!ct.IsCancellationRequested)
//            {
//                try
//                {
//                    using var scope = _sp.CreateScope();
//                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

//                    // Channel budget
//                    var approxCount = Volatile.Read(ref _inChannel);
//                    var budget = Math.Max(0, ChannelCapacity - approxCount);

//                    if (budget <= 0)
//                    {
//                        await Task.Delay(idleDelay, ct);
//                        idleDelay = TimeSpan.FromMilliseconds(
//                            Math.Min(maxIdleDelay.TotalMilliseconds, idleDelay.TotalMilliseconds * 1.25)
//                        );
//                        continue;
//                    }

//                    // We have budget; don’t carry a long backoff into a fresh poll cycle
//                    idleDelay = _pollInterval;

//                    var take = Math.Min(budget, 2000);
//                    var flightSecs = (int)Math.Ceiling(_flightTimeout.TotalSeconds);

//                    var takeParam = new NpgsqlParameter<int>("take", take);
//                    var flightParam = new NpgsqlParameter<int>("flight", flightSecs);

//                    var prevTimeout = db.Database.GetCommandTimeout();
//                    db.Database.SetCommandTimeout(5);

//                    List<OutboundMessageJob> jobs;
//                    try
//                    {
//                        jobs = await db.OutboundMessageJobs
//                            .FromSqlRaw(sql, takeParam, flightParam)
//                            .AsNoTracking()
//                            .ToListAsync(ct);
//                    }
//                    finally
//                    {
//                        db.Database.SetCommandTimeout(prevTimeout);
//                    }

//                    // Push to channel; track occupancy
//                    foreach (var job in jobs)
//                    {
//                        await _channel.Writer.WriteAsync(job, ct);
//                        Interlocked.Increment(ref _inChannel);
//                    }

//                    if (jobs.Count == 0)
//                    {
//                        consecutiveEmpty++;

//                        // No work → exponential backoff, then step up to a longer 30s sleep
//                        var jitterMs = _rand.Next(0, 200);
//                        var delay = idleDelay + TimeSpan.FromMilliseconds(jitterMs);

//                        // After several empty loops, cut DB chatter significantly
//                        if (consecutiveEmpty >= 8 && idleDelay >= maxIdleDelay)
//                            delay = longIdleDelay + TimeSpan.FromMilliseconds(jitterMs);

//                        await Task.Delay(delay, ct);

//                        if (idleDelay < maxIdleDelay)
//                        {
//                            idleDelay = TimeSpan.FromMilliseconds(
//                                Math.Min(maxIdleDelay.TotalMilliseconds, idleDelay.TotalMilliseconds * 2)
//                            );
//                        }
//                    }
//                    else
//                    {
//                        consecutiveEmpty = 0;
//                        idleDelay = _pollInterval;
//                    }
//                }
//                catch (TaskCanceledException)
//                {
//                    // normal shutdown
//                }
//                catch (Exception ex)
//                {
//                    _log.LogError(ex, "[Outbox] Producer loop error");
//                    try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch { /* ignore */ }
//                }
//            }
//        }

//        // ---- keyed concurrency gate (per sender) ---------------------------------------
//        private sealed class KeyedSemaphore
//        {
//            private readonly ConcurrentDictionary<string, SemaphoreSlim> _map = new();
//            public async Task<IDisposable> AcquireAsync(string key, int dop, CancellationToken ct)
//            {
//                var sem = _map.GetOrAdd(key, _ => new SemaphoreSlim(dop));
//                await sem.WaitAsync(ct);
//                return new Releaser(sem);
//            }
//            private sealed class Releaser : IDisposable
//            {
//                private readonly SemaphoreSlim _s;
//                public Releaser(SemaphoreSlim s) => _s = s;
//                public void Dispose() => _s.Release();
//            }
//        }
//        private static readonly KeyedSemaphore _perSenderGate = new();
//        // -------------------------------------------------------------------------------
//        private async Task ConsumeAsync(CancellationToken ct)
//        {
//            // Resolve a phone number for a recipient (Contact.Phone or AudienceMember.PhoneE164/Raw)
//            static async Task<string> ResolveRecipientPhoneAsync(AppDbContext db, Guid recipientId, CancellationToken ct2)
//            {
//                var phone = await db.CampaignRecipients
//                    .AsNoTracking()
//                    .Where(r => r.Id == recipientId)
//                    .Select(r =>
//                        r.Contact != null
//                            ? r.Contact.PhoneNumber
//                            : (r.AudienceMember != null
//                                ? (r.AudienceMember.PhoneE164 ?? r.AudienceMember.PhoneRaw)
//                                : null))
//                    .FirstOrDefaultAsync(ct2);

//                return phone ?? string.Empty;
//            }

//            while (await _channel.Reader.WaitToReadAsync(ct))
//            {
//                if (!_channel.Reader.TryRead(out var job))
//                    continue;

//                // decrement occupancy as soon as we pull from the channel
//                Interlocked.Decrement(ref _inChannel);

//                try
//                {
//                    using var scope = _sp.CreateScope();
//                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//                    var engine = scope.ServiceProvider.GetRequiredService<IMessageEngineService>();
//                    var billing = scope.ServiceProvider.GetRequiredService<IBillingIngestService>();
//                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<OutboundSenderWorker>>();
//                    var logSink = scope.ServiceProvider.GetRequiredService<ICampaignLogSink>();
//                    var limiter = scope.ServiceProvider.GetRequiredService<xbytechat.api.Infrastructure.RateLimiting.IPhoneNumberRateLimiter>();
//                    var messageLogSink = scope.ServiceProvider.GetRequiredService<IMessageLogSink>();
//                    var builder = scope.ServiceProvider.GetRequiredService<ITemplatePayloadBuilder>();
//                    var validator = scope.ServiceProvider.GetRequiredService<ICampaignSendValidator>(); // ✅

//                    // Per-sender key = provider + phoneNumberId
//                    var senderKey = $"{job.Provider}|{job.PhoneNumberId}";

//                    using (await _perSenderGate.AcquireAsync(senderKey, _perNumberDop, ct))
//                    {
//                        // Rate limit per number (token bucket via your registered limiter)
//                        var lease = await limiter.AcquireAsync(senderKey, ct);
//                        if (!lease.IsAcquired)
//                        {
//                            await Task.Delay(50, ct);
//                            continue;
//                        }

//                        // Resolve phone
//                        var recipientPhone = await ResolveRecipientPhoneAsync(db, job.RecipientId, ct);
//                        if (string.IsNullOrWhiteSpace(recipientPhone))
//                        {
//                            job.Status = "Failed";
//                            job.Attempt += 1;
//                            job.LastError = "Recipient phone not found.";
//                            var backoff1 = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
//                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff1);
//                            db.Update(job);
//                            await db.SaveChangesAsync(ct);

//                            logger.LogWarning("[Outbox] Recipient phone not found. job={JobId} recipient={RecipientId}", job.Id, job.RecipientId);
//                            MetricsRegistry.MessagesFailed.Add(1);
//                            continue;
//                        }

//                        // ===== Load template row (cached metadata for validation)
//                        var lang = job.LanguageCode ?? "en_US";
//                        var tmplRow = await db.WhatsAppTemplates
//                            .AsNoTracking()
//                            .FirstOrDefaultAsync(t =>
//                                t.BusinessId == job.BusinessId &&
//                                t.Provider == job.Provider &&
//                                t.Name == job.TemplateName &&
//                                t.LanguageCode == lang,
//                                ct);

//                        if (tmplRow == null)
//                        {
//                            job.Status = "Failed";
//                            job.Attempt += 1;
//                            job.LastError = $"Template not found: {job.TemplateName} ({lang}) for {job.Provider}.";
//                            var backoff = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
//                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
//                            db.Update(job);
//                            await db.SaveChangesAsync(ct);

//                            logger.LogWarning("[Outbox] Template not found. job={JobId} template={Template} lang={Lang} provider={Provider}",
//                                job.Id, job.TemplateName, lang, job.Provider);
//                            MetricsRegistry.MessagesFailed.Add(1);
//                            continue;
//                        }

//                        // Prefer DB header kind (normalized), fallback to job.MediaType
//                        HeaderKind headerKind = tmplRow.HeaderKind?.ToLowerInvariant() switch
//                        {
//                            "text" => HeaderKind.Text,
//                            "image" => HeaderKind.Image,
//                            "video" => HeaderKind.Video,
//                            "document" => HeaderKind.Document,
//                            "none" or null => HeaderKind.None,
//                            _ => HeaderKind.None
//                        };
//                        if (headerKind == HeaderKind.None && !string.IsNullOrWhiteSpace(job.MediaType))
//                        {
//                            headerKind = job.MediaType.ToLowerInvariant() switch
//                            {
//                                "text" => HeaderKind.Text,
//                                "image" => HeaderKind.Image,
//                                "video" => HeaderKind.Video,
//                                "document" => HeaderKind.Document,
//                                _ => HeaderKind.None
//                            };
//                        }

//                        // ===== Build plan & recipient
//                        var providerEnum = ProviderUtil.Parse(job.Provider);

//                        // NEW: hydrate ButtonMeta list from the template row (cached)
//                        var buttonsFromTemplate = GetTemplateButtonsCached(tmplRow);

//                        var plan = new SendPlan(
//                            BusinessId: job.BusinessId,
//                            Provider: providerEnum,
//                            PhoneNumberId: job.PhoneNumberId!,
//                            TemplateName: job.TemplateName!,
//                            LanguageCode: lang,
//                            HeaderKind: headerKind,
//                            HeaderUrl: job.HeaderMediaUrl,
//                            Buttons: buttonsFromTemplate // <-- previously Array.Empty<ButtonMeta>()
//                        );

//                        var recipient = new RecipientPlan(
//                            RecipientId: job.RecipientId,
//                            ToPhoneE164: recipientPhone,
//                            ParametersJson: job.ResolvedParamsJson ?? "[]",
//                            ButtonParamsJson: job.ResolvedButtonUrlsJson,
//                            IdempotencyKey: job.IdempotencyKey ?? $"{job.CampaignId}:{recipientPhone}:{job.TemplateName}"
//                        );

//                        // ===== Build envelope (generic) then VALIDATE before provider mapping
//                        var envelope = builder.Build(plan, recipient); // TemplateEnvelope

//                        // ICampaignSendValidator.Validate(plan, recipient, envelope, tmplRow)
//                        var (ok, error) = validator.Validate(plan, recipient, envelope, tmplRow);
//                        if (!ok)
//                        {
//                            job.Status = "Failed";
//                            job.Attempt += 1;
//                            job.LastError = error ?? "Validation failed.";
//                            var backoff = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
//                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
//                            db.Update(job);
//                            await db.SaveChangesAsync(ct);

//                            logger.LogWarning("[Outbox] Validation failed job={JobId} error={Error}", job.Id, job.LastError);
//                            MetricsRegistry.MessagesFailed.Add(1);
//                            continue;
//                        }

//                        // ===== Map to provider payload AFTER validation
//                        object payload = providerEnum switch
//                        {
//                            Provider.MetaCloud =>
//                                scope.ServiceProvider.GetRequiredService<MetaCloudPayloadMapper>()
//                                    .BuildPayload(plan, recipient, envelope),

//                            Provider.Pinnacle =>
//                                scope.ServiceProvider.GetRequiredService<PinnaclePayloadMapper>()
//                                    .BuildPayload(plan, recipient, envelope),

//                            _ => throw new InvalidOperationException("Unknown provider")
//                        };
//                        // ===== End unified build =====

//                        // --- Send via engine + metrics timers ---
//                        var sw = Stopwatch.StartNew();
//                        var engineResult = await engine.SendPayloadAsync(job.BusinessId, job.Provider, payload, job.PhoneNumberId);
//                        sw.Stop();
//                        MetricsRegistry.SendLatencyMs.Record(sw.Elapsed.TotalMilliseconds);

//                        // Adaptive nudge on 429 (based on error text)
//                        if (!engineResult.Success)
//                        {
//                            var err = engineResult.ErrorMessage ?? string.Empty;
//                            if (err.Contains("429", StringComparison.Ordinal) ||
//                                err.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
//                            {
//                                limiter.UpdateLimits(senderKey, permitsPerSecond: 5, burst: 5);
//                                MetricsRegistry.RateLimited429s.Add(1);
//                            }
//                        }

//                        // Persist MessageLogs (COPY sink)
//                        var now = DateTime.UtcNow;
//                        var runId = Guid.NewGuid();
//                        var logId = Guid.NewGuid();

//                        messageLogSink.Enqueue(new MessageLog
//                        {
//                            Id = logId,
//                            BusinessId = job.BusinessId,
//                            CampaignId = job.CampaignId,
//                            RecipientNumber = recipientPhone,
//                            MessageContent = job.TemplateName,
//                            MediaUrl = job.HeaderMediaUrl,
//                            Status = engineResult.Success ? "Sent" : "Failed",
//                            MessageId = engineResult.MessageId,
//                            ErrorMessage = engineResult.ErrorMessage,
//                            RawResponse = engineResult.RawResponse,
//                            CreatedAt = now,
//                            SentAt = engineResult.Success ? now : (DateTime?)null,
//                            Source = "campaign",
//                            RunId = runId,
//                            Provider = job.Provider,
//                            ProviderMessageId = engineResult.MessageId,
//                            IsIncoming = false,
//                            IsChargeable=false
//                        });

//                        // Batched CampaignSendLog (COPY via sink)
//                        logSink.Enqueue(new CampaignLogRecord(
//                            Id: Guid.NewGuid(),
//                            RunId: runId,
//                            MessageId: engineResult.MessageId,
//                            CampaignId: job.CampaignId,
//                            ContactId: null,
//                            RecipientId: job.RecipientId,
//                            MessageBody: job.MessageBody ?? job.TemplateName,
//                            TemplateId: job.TemplateName,
//                            SendStatus: engineResult.Success ? "Sent" : "Failed",
//                            ErrorMessage: engineResult.ErrorMessage,
//                            CreatedAt: now,
//                            CreatedBy: "system",
//                            SentAt: engineResult.Success ? now : (DateTime?)null,
//                            DeliveredAt: null,
//                            ReadAt: null,
//                            IpAddress: null,
//                            DeviceInfo: null,
//                            MacAddress: null,
//                            SourceChannel: "campaign",
//                            DeviceType: null,
//                            Browser: null,
//                            Country: null,
//                            City: null,
//                            IsClicked: false,
//                            ClickedAt: null,
//                            ClickType: null,
//                            RetryCount: job.Attempt,
//                            LastRetryAt: now,
//                            LastRetryStatus: engineResult.Success ? "Success" : "Failed",
//                            AllowRetry: job.Attempt < DEFAULT_MAX_ATTEMPTS,
//                            MessageLogId: logId,
//                            BusinessId: job.BusinessId,
//                            CTAFlowConfigId: null,
//                            CTAFlowStepId: null,
//                            ButtonBundleJson: null
//                        ));

//                        // Update job (retry/backoff if needed)
//                        job.Status = engineResult.Success ? "Sent" : "Failed";
//                        job.Attempt += 1;
//                        job.LastError = engineResult.ErrorMessage;

//                        if (!engineResult.Success)
//                        {
//                            var backoffSecs = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
//                            job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSecs);
//                            MetricsRegistry.MessagesFailed.Add(1);
//                        }
//                        else
//                        {
//                            job.NextAttemptAt = null;
//                            MetricsRegistry.MessagesSent.Add(1);
//                        }

//                        db.Update(job);
//                        await db.SaveChangesAsync(ct);

//                        // Async billing ingest (complete before scope disposed)
//                        await billing.IngestFromSendResponseAsync(
//                            job.BusinessId,
//                            logId,
//                            job.Provider,
//                            engineResult.RawResponse ?? "{}"
//                        );
//                    }
//                }
//                catch (TaskCanceledException)
//                {
//                    // graceful shutdown
//                }
//                catch (Exception ex)
//                {
//                    try
//                    {
//                        using var scope = _sp.CreateScope();
//                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//                        job.Status = "Failed";
//                        job.Attempt += 1;
//                        job.LastError = ex.Message;
//                        var backoffSecs = (int)Math.Min(60, Math.Pow(2, job.Attempt) * 2);
//                        job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSecs);
//                        db.Update(job);
//                        await db.SaveChangesAsync(ct);
//                    }
//                    catch { /* swallow */ }

//                    MetricsRegistry.MessagesFailed.Add(1);
//                    _log.LogError(ex, "[Outbox] Consume error job={JobId}", job.Id);
//                }
//            }
//        }
//    }
//}






