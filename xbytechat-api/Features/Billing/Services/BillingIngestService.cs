using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api; // AppDbContext
using xbytechat_api.Features.Billing.Models;
using Npgsql;
using Serilog;

namespace xbytechat_api.Features.Billing.Services
{
    public class BillingIngestService : IBillingIngestService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<BillingIngestService> _log;

        public BillingIngestService(AppDbContext db, ILogger<BillingIngestService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task IngestFromSendResponseAsync(Guid businessId, Guid messageLogId, string provider, string rawResponseJson)
        {
            // Guard: only accept events for known businesses
            var hasBiz = await _db.Businesses.AnyAsync(b => b.Id == businessId);
            if (!hasBiz)
            {
                _log.LogWarning("Ignoring send-response for unknown business {BusinessId}", businessId);
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(rawResponseJson);

                string? providerMessageId =
                    doc.RootElement.TryGetProperty("messages", out var msgs) &&
                    msgs.ValueKind == JsonValueKind.Array &&
                    msgs.GetArrayLength() > 0
                        ? (msgs[0].TryGetProperty("id", out var idEl) ? idEl.GetString() : null)
                        : (doc.RootElement.TryGetProperty("id", out var idEl2) ? idEl2.GetString() : null);

                var logRow = await _db.MessageLogs
                    .FirstOrDefaultAsync(x => x.Id == messageLogId && x.BusinessId == businessId);

                if (logRow != null)
                {
                    logRow.Provider = provider;
                    if (!string.IsNullOrWhiteSpace(providerMessageId))
                        logRow.ProviderMessageId = providerMessageId;
                }

                var ev = new ProviderBillingEvent
                {
                    BusinessId = businessId,
                    MessageLogId = messageLogId,
                    Provider = provider,
                    EventType = "send_response",
                    ProviderMessageId = providerMessageId,
                    PayloadJson = rawResponseJson,
                    OccurredAt = DateTimeOffset.UtcNow
                };

                _db.ProviderBillingEvents.Add(ev);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to ingest send response payload for business {BusinessId}", businessId);
            }
        }

        public async Task IngestFromWebhookAsync(Guid businessId, string provider, string payloadJson)
        {
            // Guard: only accept events for known businesses
            var hasBiz = await _db.Businesses.AnyAsync(b => b.Id == businessId);
            if (!hasBiz)
            {
                _log.LogWarning("Ignoring {Provider} webhook for unknown business {BusinessId}", provider, businessId);
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var now = DateTimeOffset.UtcNow;

                // For the common/strong-keyed case (ProviderMessageId), we rely on DB unique index.
                // For weaker cases (no ProviderMessageId, only ConversationId), we still probe with ExistsAsync.

                Task<bool> ExistsForConversationAsync(string eventType, string? conversationId)
                {
                    if (string.IsNullOrWhiteSpace(conversationId))
                        return Task.FromResult(false);

                    return _db.ProviderBillingEvents.AsNoTracking().AnyAsync(x =>
                        x.BusinessId == businessId &&
                        x.Provider == provider &&
                        x.EventType == eventType &&
                        x.ConversationId == conversationId);
                }

                if (string.Equals(provider, "META_CLOUD", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var entry in Enumerate(doc.RootElement, "entry"))
                        foreach (var change in Enumerate(entry, "changes"))
                        {
                            if (!change.TryGetProperty("value", out var value))
                                continue;

                            foreach (var st in Enumerate(value, "statuses"))
                            {
                                string? providerMessageId = st.TryGetProperty("id", out var idEl)
                                    ? idEl.GetString()
                                    : null;

                                string? status = null;
                                if (st.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
                                    status = statusEl.GetString()?.ToLowerInvariant();

                                // OccurredAt from provider if present
                                var occurredAt = now;
                                if (st.TryGetProperty("timestamp", out var tsEl))
                                {
                                    if (tsEl.ValueKind == JsonValueKind.String &&
                                        long.TryParse(tsEl.GetString(), out var tsLong))
                                        occurredAt = DateTimeOffset.FromUnixTimeSeconds(tsLong);
                                    else if (tsEl.ValueKind == JsonValueKind.Number &&
                                             tsEl.TryGetInt64(out var tsNum))
                                        occurredAt = DateTimeOffset.FromUnixTimeSeconds(tsNum);
                                }

                                // Conversation info
                                string? conversationId = null;
                                DateTimeOffset? convStartedAt = null;

                                if (st.TryGetProperty("conversation", out var convEl) &&
                                    convEl.ValueKind == JsonValueKind.Object)
                                {
                                    if (convEl.TryGetProperty("id", out var cidEl))
                                        conversationId = cidEl.GetString();

                                    if (convEl.TryGetProperty("expiration_timestamp", out var expEl))
                                    {
                                        long exp = 0;
                                        if (expEl.ValueKind == JsonValueKind.String &&
                                            long.TryParse(expEl.GetString(), out var expStr))
                                            exp = expStr;
                                        else if (expEl.ValueKind == JsonValueKind.Number &&
                                                 expEl.TryGetInt64(out var expNum))
                                            exp = expNum;

                                        if (exp > 0)
                                        {
                                            var expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
                                            convStartedAt = expiration.AddHours(-24);
                                        }
                                    }
                                }

                                // Pricing block (optional)
                                string? category = null;
                                bool? billable = null;
                                decimal? amount = null;
                                string? currency = null;

                                if (st.TryGetProperty("pricing", out var pEl) &&
                                    pEl.ValueKind == JsonValueKind.Object)
                                {
                                    if (pEl.TryGetProperty("category", out var catEl))
                                        category = catEl.GetString()?.ToLowerInvariant();

                                    if (pEl.TryGetProperty("billable", out var bilEl) &&
                                        (bilEl.ValueKind == JsonValueKind.True ||
                                         bilEl.ValueKind == JsonValueKind.False))
                                        billable = bilEl.GetBoolean();

                                    if (pEl.TryGetProperty("amount", out var amtEl) &&
                                        amtEl.ValueKind == JsonValueKind.Number)
                                        amount = amtEl.GetDecimal();

                                    if (pEl.TryGetProperty("currency", out var curEl) &&
                                        curEl.ValueKind == JsonValueKind.String)
                                        currency = curEl.GetString();
                                }

                                // 1) Status event (if present)
                                if (!string.IsNullOrWhiteSpace(status))
                                {
                                    var ev = new ProviderBillingEvent
                                    {
                                        BusinessId = businessId,
                                        Provider = provider,
                                        EventType = status, // sent/delivered/read etc.
                                        ProviderMessageId = providerMessageId,
                                        ConversationId = conversationId,
                                        ConversationCategory = category,
                                        IsChargeable = billable,
                                        PriceAmount = amount,
                                        PriceCurrency = currency,
                                        PayloadJson = payloadJson,
                                        OccurredAt = occurredAt
                                    };

                                    if (!string.IsNullOrWhiteSpace(providerMessageId))
                                    {
                                        await TryAddBillingEventAsync(ev);
                                    }
                                    else
                                    {
                                        // fallback: dedupe via conversation id if no provider message id
                                        if (!await ExistsForConversationAsync(status, conversationId))
                                        {
                                            _db.ProviderBillingEvents.Add(ev);
                                        }
                                    }
                                }

                                // 2) Pricing update (if any pricing fields)
                                var hasAnyPricing =
                                    !string.IsNullOrWhiteSpace(category) ||
                                    billable.HasValue ||
                                    amount.HasValue ||
                                    !string.IsNullOrWhiteSpace(currency);

                                if (hasAnyPricing)
                                {
                                    var pricingEv = new ProviderBillingEvent
                                    {
                                        BusinessId = businessId,
                                        Provider = provider,
                                        EventType = "pricing_update",
                                        ProviderMessageId = providerMessageId,
                                        ConversationId = conversationId,
                                        ConversationCategory = category,
                                        IsChargeable = billable,
                                        PriceAmount = amount,
                                        PriceCurrency = currency,
                                        PayloadJson = payloadJson,
                                        OccurredAt = occurredAt
                                    };

                                    if (!string.IsNullOrWhiteSpace(providerMessageId))
                                    {
                                        await TryAddBillingEventAsync(pricingEv);
                                    }
                                    else
                                    {
                                        if (!await ExistsForConversationAsync("pricing_update", conversationId))
                                        {
                                            _db.ProviderBillingEvents.Add(pricingEv);
                                        }
                                    }
                                }

                                // Keep MessageLog in sync (best effort)
                                var logRow = await FindMatchingMessageLog(businessId, providerMessageId, conversationId);
                                if (logRow != null)
                                {
                                    logRow.Provider = provider;
                                    if (!string.IsNullOrWhiteSpace(providerMessageId))
                                        logRow.ProviderMessageId = providerMessageId;
                                    if (!string.IsNullOrWhiteSpace(conversationId))
                                        logRow.ConversationId = conversationId;
                                    if (convStartedAt.HasValue)
                                        logRow.ConversationStartedAt = convStartedAt;

                                    if (billable.HasValue)
                                        logRow.IsChargeable = billable.Value;
                                    if (!string.IsNullOrWhiteSpace(category))
                                        logRow.ConversationCategory = category;
                                    if (amount.HasValue)
                                        logRow.PriceAmount = amount;
                                    if (!string.IsNullOrWhiteSpace(currency))
                                        logRow.PriceCurrency = currency;
                                }
                            }
                        }
                }
                else if (string.Equals(provider, "PINNACLE", StringComparison.OrdinalIgnoreCase))
                {
                    // Best-effort scan for pricing blocks
                    foreach (var pricing in JsonPathAll(doc.RootElement, "pricing"))
                    {
                        string? category = pricing.TryGetProperty("category", out var catEl)
                            ? catEl.GetString()?.ToLowerInvariant()
                            : null;

                        bool? billable =
                            pricing.TryGetProperty("billable", out var bilEl) &&
                            (bilEl.ValueKind == JsonValueKind.True ||
                             bilEl.ValueKind == JsonValueKind.False)
                                ? bilEl.GetBoolean()
                                : (bool?)null;

                        decimal? amount = null;
                        if (pricing.TryGetProperty("amount", out var amtEl) &&
                            amtEl.ValueKind == JsonValueKind.Number)
                            amount = amtEl.GetDecimal();

                        string? currency = pricing.TryGetProperty("currency", out var curEl)
                            ? curEl.GetString()
                            : null;

                        var parent = TryGetParentObject(doc.RootElement, pricing);
                        string? providerMessageId =
                            TryGetString(parent, "id")
                            ?? TryGetString(parent, "message_id")
                            ?? TryGetString(parent, "wamid");

                        string? conversationId =
                            TryGetString(parent, "conversation_id")
                            ?? TryGetNestedString(parent, "conversation", "id");

                        string? status = TryGetString(parent, "status")?.ToLowerInvariant();

                        // Pricing event
                        var pricingEv = new ProviderBillingEvent
                        {
                            BusinessId = businessId,
                            Provider = provider,
                            EventType = "pricing_update",
                            ProviderMessageId = providerMessageId,
                            ConversationId = conversationId,
                            ConversationCategory = category,
                            IsChargeable = billable,
                            PriceAmount = amount,
                            PriceCurrency = currency,
                            PayloadJson = payloadJson,
                            OccurredAt = now
                        };

                        if (!string.IsNullOrWhiteSpace(providerMessageId))
                        {
                            await TryAddBillingEventAsync(pricingEv);
                        }
                        else
                        {
                            if (!await ExistsForConversationAsync("pricing_update", conversationId))
                                _db.ProviderBillingEvents.Add(pricingEv);
                        }

                        // Optional status event from same parent
                        if (!string.IsNullOrWhiteSpace(status))
                        {
                            var statusEv = new ProviderBillingEvent
                            {
                                BusinessId = businessId,
                                Provider = provider,
                                EventType = status,
                                ProviderMessageId = providerMessageId,
                                ConversationId = conversationId,
                                ConversationCategory = category,
                                IsChargeable = billable,
                                PriceAmount = amount,
                                PriceCurrency = currency,
                                PayloadJson = payloadJson,
                                OccurredAt = now
                            };

                            if (!string.IsNullOrWhiteSpace(providerMessageId))
                            {
                                await TryAddBillingEventAsync(statusEv);
                            }
                            else
                            {
                                if (!await ExistsForConversationAsync(status, conversationId))
                                    _db.ProviderBillingEvents.Add(statusEv);
                            }
                        }

                        // Sync MessageLog where possible
                        var logRow = await FindMatchingMessageLog(businessId, providerMessageId, conversationId);
                        if (logRow != null)
                        {
                            logRow.Provider = provider;
                            if (!string.IsNullOrWhiteSpace(providerMessageId))
                                logRow.ProviderMessageId = providerMessageId;
                            if (!string.IsNullOrWhiteSpace(conversationId))
                                logRow.ConversationId = conversationId;

                            if (billable.HasValue)
                                logRow.IsChargeable = billable.Value;
                            if (!string.IsNullOrWhiteSpace(category))
                                logRow.ConversationCategory = category;
                            if (amount.HasValue)
                                logRow.PriceAmount = amount;
                            if (!string.IsNullOrWhiteSpace(currency))
                                logRow.PriceCurrency = currency;
                        }
                    }
                }
                else
                {
                    // Unknown provider; keep audit trail, but flagged
                    _db.ProviderBillingEvents.Add(new ProviderBillingEvent
                    {
                        BusinessId = businessId,
                        Provider = provider,
                        EventType = "unknown_provider_webhook",
                        PayloadJson = payloadJson,
                        OccurredAt = now
                    });
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to ingest webhook payload for business {BusinessId}", businessId);
            }
        }

        // -------- helpers --------



        private async Task<ProviderBillingEvent?> TryAddBillingEventAsync(ProviderBillingEvent ev)
        {
            if (string.IsNullOrWhiteSpace(ev.ProviderMessageId))
            {
                // No strong key; let caller handle SaveChanges once.
                _db.ProviderBillingEvents.Add(ev);
                return ev;
            }

            _db.ProviderBillingEvents.Add(ev);

            try
            {
                await _db.SaveChangesAsync();
                return ev;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _log.LogDebug("Duplicate ProviderBillingEvent ignored for message {ProviderMessageId}", ev.ProviderMessageId);
                _db.Entry(ev).State = EntityState.Detached;
                return null;
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            // Postgres example
            if (ex.InnerException is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }

            // For SQL Server / others, check error numbers here.

            return false;
        }

        private async Task<MessageLog?> FindMatchingMessageLog(Guid businessId, string? providerMessageId, string? conversationId)
        {
            if (!string.IsNullOrWhiteSpace(providerMessageId))
            {
                var byMsgId = await _db.MessageLogs
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(x =>
                        x.BusinessId == businessId &&
                        x.ProviderMessageId == providerMessageId);
                if (byMsgId != null) return byMsgId;
            }

            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                var byConv = await _db.MessageLogs
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(x =>
                        x.BusinessId == businessId &&
                        x.ConversationId == conversationId);
                if (byConv != null) return byConv;
            }

            return null;
        }

        private static IEnumerable<JsonElement> Enumerate(JsonElement root, string name)
        {
            if (root.ValueKind != JsonValueKind.Object) yield break;
            if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;

            foreach (var x in arr.EnumerateArray())
                yield return x;
        }

        private static IEnumerable<JsonElement> JsonPathAll(JsonElement root, string name)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in root.EnumerateObject())
                {
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                        yield return p.Value;

                    foreach (var x in JsonPathAll(p.Value, name))
                        yield return x;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    foreach (var x in JsonPathAll(item, name))
                        yield return x;
            }
        }

        private static JsonElement? TryGetParentObject(JsonElement root, JsonElement node)
        {
            // Best-effort: System.Text.Json has no parent pointer; we scan recursively.
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in root.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (object.ReferenceEquals(p.Value, node)) return root;
                        var cand = TryGetParentObject(p.Value, node);
                        if (cand.HasValue) return cand;
                    }
                    else if (p.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in p.Value.EnumerateArray())
                        {
                            if (object.ReferenceEquals(e, node)) return root;
                            var cand = TryGetParentObject(e, node);
                            if (cand.HasValue) return cand;
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in root.EnumerateArray())
                {
                    var cand = TryGetParentObject(e, node);
                    if (cand.HasValue) return cand;
                }
            }

            return null;
        }

        private static string? TryGetString(JsonElement? obj, string name)
        {
            if (!obj.HasValue || obj.Value.ValueKind != JsonValueKind.Object)
                return null;

            return obj.Value.TryGetProperty(name, out var el) ? el.GetString() : null;
        }

        private static string? TryGetNestedString(JsonElement? obj, string name1, string name2)
        {
            if (!obj.HasValue || obj.Value.ValueKind != JsonValueKind.Object)
                return null;

            if (!obj.Value.TryGetProperty(name1, out var inner) || inner.ValueKind != JsonValueKind.Object)
                return null;

            return inner.TryGetProperty(name2, out var v) ? v.GetString() : null;
        }
    }
}
