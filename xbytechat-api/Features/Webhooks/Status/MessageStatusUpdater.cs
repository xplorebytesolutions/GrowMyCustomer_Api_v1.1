using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// 👇 make sure this is where your AppDbContext lives
using xbytechat.api;

using xbytechat.api.Features.CampaignTracking.Models; // CampaignSendLog
using xbytechat.api.Features.CampaignModule.Models;   // Campaign (nav)
using xbytechat.api.Features.CRM.Models;                      // Contact (nav)
using xbytechat.api.Features.MessageManagement.DTOs;  // MessageLog

// 👇 Billing ingest
using xbytechat_api.Features.Billing.Services;        // IBillingIngestService
using xbytechat.api.Infrastructure.Observability;
using Channel = System.Threading.Channels.Channel;
using System.Threading.RateLimiting;
using Serilog;
namespace xbytechat.api.Features.Webhooks.Status
{
    /// <summary>
    /// Idempotent updater touching CampaignSendLogs and MessageLogs using your actual schema.
    /// Also forwards raw Meta webhook payloads to Billing ingest for pricing/billing capture.
    /// </summary>
    public class MessageStatusUpdater : IMessageStatusUpdater
    {
        private readonly AppDbContext _db;
        private readonly ILogger<MessageStatusUpdater> _log;
        private readonly IBillingIngestService _billing;

        public MessageStatusUpdater(AppDbContext db,
                                    ILogger<MessageStatusUpdater> log,
                                    IBillingIngestService billing)
        {
            _db = db;
            _log = log;
            _billing = billing;
        }

        //public async Task UpdateAsync(StatusEvent ev, CancellationToken ct = default)
        //{
        //    // 🔎 Guard: we need Business + ProviderMessageId (WAMID) to reconcile reliably
        //    if (ev.BusinessId == Guid.Empty || string.IsNullOrWhiteSpace(ev.ProviderMessageId))
        //    {
        //        _log.LogWarning("Status update missing key fields (BusinessId or ProviderMessageId). Skip.");
        //        return;
        //    }

        //    // 1) Pull candidates (scoped to business + WAMID)
        //    var sendLogQ = _db.Set<CampaignSendLog>()
        //                      .AsTracking()
        //                      .Where(s => s.BusinessId == ev.BusinessId && s.MessageId == ev.ProviderMessageId);

        //    // NOTE: some rows set both MessageId and ProviderMessageId to the wamid; be flexible.
        //    var msgLogQ = _db.Set<MessageLog>()
        //                     .AsTracking()
        //                     .Where(m => m.BusinessId == ev.BusinessId &&
        //                                (m.ProviderMessageId == ev.ProviderMessageId ||
        //                                 m.MessageId == ev.ProviderMessageId));

        //    // If caller passed a specific CampaignSendLogId, narrow further
        //    if (ev.CampaignSendLogId is Guid sid)
        //        sendLogQ = sendLogQ.Where(s => s.Id == sid);

        //    var sendLog = await sendLogQ.FirstOrDefaultAsync(ct);
        //    var msgLog = await msgLogQ.FirstOrDefaultAsync(ct);

        //    // 2) Apply transition (idempotent)
        //    var changed = ApplyTransition(sendLog, msgLog, ev);

        //    // 3) Persist only if something actually changed
        //    if (changed > 0)
        //        await _db.SaveChangesAsync(ct);

        //    // 4) Always forward Meta status webhook payloads to Billing ingest (for pricing events).
        //    await TryForwardToBillingAsync(ev, ct);
        //}


        public async Task<int> UpdateAsync(string messageId, string status, DateTime tsUtc, string? error, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(messageId)) return 0;

            status = status.Trim().ToLowerInvariant();

            // 1) Update CampaignSendLogs by MessageId (idempotent)
            var cslQuery = _db.CampaignSendLogs.Where(x => x.MessageId == messageId);

            int affected;
            if (status == "delivered")
            {
                affected = await cslQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error)
                    .SetProperty(x => x.DeliveredAt, tsUtc)
                    .SetProperty(x => x.SendStatus, "Delivered")
                    .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc), ct);
            }
            else if (status == "read")
            {
                affected = await cslQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error)
                    .SetProperty(x => x.ReadAt, tsUtc)
                    .SetProperty(x => x.SendStatus, "Read")
                    .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc)
                    .SetProperty(x => x.DeliveredAt, x => x.DeliveredAt ?? tsUtc), ct);
            }
            else if (status == "failed")
            {
                affected = await cslQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error)
                    .SetProperty(x => x.SendStatus, "Failed")
                    .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc), ct);
            }
            else if (status == "sent")
            {
                affected = await cslQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error)
                    .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc)
                    .SetProperty(x => x.SendStatus, "Sent"), ct);
            }
            else
            {
                // Unknown status: only persist the error text (harmless)
                affected = await cslQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error), ct);
            }

            // 2) Mirror onto MessageLogs (optional but useful for parity)
            var mlQuery = _db.MessageLogs.Where(m => m.ProviderMessageId == messageId);

            if (status == "delivered")
            {
                await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Delivered")
                    .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }
            else if (status == "read")
            {
                await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Read")
                    .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }
            else if (status == "failed")
            {
                await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Failed")
                    .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }
            else if (status == "sent")
            {
                await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Sent")
                    .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }
            // else: ignore unknown statuses for MessageLogs

            // 3) Side-effects & metrics
            if (affected == 0)
            {
                _log.LogWarning("[StatusUpdater] No CampaignSendLog row for messageId={MessageId}", messageId);
            }
            else
            {
                if (status == "failed") MetricsRegistry.MessagesFailed.Add(1);
                else if (status == "sent") MetricsRegistry.MessagesSent.Add(1);
                // delivered/read are visible in analytics; counters optional
            }

            // Optional: billing for delivered/read goes here if required

            return affected;
        }

        /// <summary>Returns number of entities modified.</summary>
        private int ApplyTransition(CampaignSendLog? sendLog, MessageLog? msgLog, StatusEvent ev)
        {
            int modified = 0;

            // --- CampaignSendLog updates ---
            if (sendLog != null)
            {
                if (!string.Equals(sendLog.MessageId, ev.ProviderMessageId, StringComparison.Ordinal))
                {
                    sendLog.MessageId = ev.ProviderMessageId;
                    modified++;
                }

                switch (ev.State)
                {
                    case MessageDeliveryState.Sent:
                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Sent"))
                        {
                            sendLog.SendStatus = "Sent";
                            modified++;
                        }
                        if (sendLog.SentAt == null || sendLog.SentAt == default)
                            sendLog.SentAt = ev.OccurredAt.UtcDateTime;
                        break;

                    case MessageDeliveryState.Delivered:
                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Read") &&
                            !EqualsIgnoreCase(sendLog.SendStatus, "Delivered"))
                        {
                            sendLog.SendStatus = "Delivered";
                            modified++;
                        }
                        if (sendLog.DeliveredAt == null || sendLog.DeliveredAt == default)
                            sendLog.DeliveredAt = ev.OccurredAt.UtcDateTime;
                        break;

                    case MessageDeliveryState.Read:
                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Read"))
                        {
                            sendLog.SendStatus = "Read";
                            modified++;
                        }
                        if (sendLog.ReadAt == null || sendLog.ReadAt == default)
                            sendLog.ReadAt = ev.OccurredAt.UtcDateTime;
                        break;

                    case MessageDeliveryState.Failed:
                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Failed"))
                        {
                            sendLog.SendStatus = "Failed";
                            modified++;
                        }
                        if (sendLog.ErrorMessage != ev.ErrorMessage)
                        {
                            sendLog.ErrorMessage = ev.ErrorMessage;
                            modified++;
                        }
                        break;

                    case MessageDeliveryState.Deleted:
                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Deleted"))
                        {
                            sendLog.SendStatus = "Deleted";
                            modified++;
                        }
                        break;
                }
            }

            // --- MessageLog updates ---
            if (msgLog != null)
            {
                if (!string.Equals(msgLog.MessageId, ev.ProviderMessageId, StringComparison.Ordinal))
                {
                    msgLog.MessageId = ev.ProviderMessageId;
                    modified++;
                }

                switch (ev.State)
                {
                    case MessageDeliveryState.Sent:
                        if (!EqualsIgnoreCase(msgLog.Status, "Sent"))
                        {
                            msgLog.Status = "Sent";
                            modified++;
                        }
                        if (msgLog.SentAt == null || msgLog.SentAt == default)
                            msgLog.SentAt = ev.OccurredAt.UtcDateTime;
                        break;

                    case MessageDeliveryState.Delivered:
                        if (!EqualsIgnoreCase(msgLog.Status, "Read") &&
                            !EqualsIgnoreCase(msgLog.Status, "Delivered"))
                        {
                            msgLog.Status = "Delivered";
                            modified++;
                        }
                        break;

                    case MessageDeliveryState.Read:
                        if (!EqualsIgnoreCase(msgLog.Status, "Read"))
                        {
                            msgLog.Status = "Read";
                            modified++;
                        }
                        break;

                    case MessageDeliveryState.Failed:
                        if (!EqualsIgnoreCase(msgLog.Status, "Failed"))
                        {
                            msgLog.Status = "Failed";
                            modified++;
                        }
                        if (msgLog.ErrorMessage != ev.ErrorMessage)
                        {
                            msgLog.ErrorMessage = ev.ErrorMessage;
                            modified++;
                        }
                        break;

                    case MessageDeliveryState.Deleted:
                        if (!EqualsIgnoreCase(msgLog.Status, "Deleted"))
                        {
                            msgLog.Status = "Deleted";
                            modified++;
                        }
                        break;
                }
            }

            if (sendLog == null && msgLog == null)
            {
                _log.LogWarning("No matching rows for BusinessId={BusinessId}, MessageId={MessageId}, State={State}",
                    ev.BusinessId, ev.ProviderMessageId, ev.State);
            }

            return modified;
        }

        private static bool EqualsIgnoreCase(string? a, string? b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // ---------------- Billing forwarder ----------------

        private async Task TryForwardToBillingAsync(StatusEvent ev, CancellationToken ct)
        {
            try
            {
                if (ev.BusinessId == Guid.Empty) return;

                // Pull Provider (via reflection to avoid changing your StatusEvent contract)
                var provider = GetStringProp(ev, "Provider")
                               ?? GetStringProp(ev, "ChannelProvider")
                               ?? GetStringProp(ev, "SourceProvider")
                               ?? GetStringProp(ev, "ProviderNormalized");

                // Try to get raw JSON payload from common property names
                string? rawJson =
                    GetStringProp(ev, "RawPayloadJson") ??
                    GetStringProp(ev, "PayloadJson") ??
                    TryGetJsonElementText(ev, "Body") ??
                    TryGetJsonElementText(ev, "RawBody");

                // If provider missing, use a lightweight sniff (Meta sends "whatsapp_business_account")
                if (string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(rawJson) &&
                    rawJson.IndexOf("\"whatsapp_business_account\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    provider = "META_CLOUD";
                }

                // Normalize provider for billing
                var normalized = NormalizeProvider(provider);
                if (normalized != "META_CLOUD") return; // only forward Meta to billing ingest for now

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    _log.LogDebug("Billing forward skipped: no raw payload JSON available on StatusEvent.");
                    return;
                }

                await _billing.IngestFromWebhookAsync(ev.BusinessId, normalized, rawJson);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Billing ingest (status webhook) failed. businessId={BusinessId}", ev.BusinessId);
            }
        }

        private static string NormalizeProvider(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return "";
            var p = provider.Trim();
            if (p.Equals("META_CLOUD", StringComparison.OrdinalIgnoreCase)) return "META_CLOUD";
            if (p.Equals("meta", StringComparison.OrdinalIgnoreCase)) return "META_CLOUD";
            if (p.Equals("meta_cloud", StringComparison.OrdinalIgnoreCase)) return "META_CLOUD";
            return p; // other providers unchanged
        }

        private static string? GetStringProp(object obj, string propName)
        {
            var pi = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) return null;
            var val = pi.GetValue(obj);
            return val as string;
        }

        private static string? TryGetJsonElementText(object obj, string propName)
        {
            var pi = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) return null;
            var val = pi.GetValue(obj);
            if (val is JsonElement je) return je.GetRawText();
            return null;
        }
    }
}


//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;

//// 👇 make sure this is where your AppDbContext lives
//using xbytechat.api;

//using xbytechat.api.Features.CampaignTracking.Models; // CampaignSendLog
//using xbytechat.api.Features.CampaignModule.Models;   // Campaign (nav)
//using xbytechat.api.CRM.Models;                       // Contact (nav)
//using xbytechat.api.Features.MessageManagement.DTOs;  // MessageLog

//namespace xbytechat.api.Features.Webhooks.Status
//{
//    /// <summary>
//    /// Idempotent updater touching CampaignSendLogs and MessageLogs using your actual schema.
//    /// </summary>
//    public class MessageStatusUpdater : IMessageStatusUpdater
//    {
//        private readonly AppDbContext _db;
//        private readonly ILogger<MessageStatusUpdater> _log;

//        public MessageStatusUpdater(AppDbContext db, ILogger<MessageStatusUpdater> log)
//        {
//            _db = db;
//            _log = log;
//        }

//        public async Task UpdateAsync(StatusEvent ev, CancellationToken ct = default)
//        {
//            // 🔎 Guard: we need Business + ProviderMessageId (WAMID) to reconcile reliably
//            if (ev.BusinessId == Guid.Empty || string.IsNullOrWhiteSpace(ev.ProviderMessageId))
//            {
//                _log.LogWarning("Status update missing key fields (BusinessId or ProviderMessageId). Skip.");
//                return;
//            }

//            // 1) Pull candidates (scoped to business + WAMID)
//            var sendLogQ = _db.Set<CampaignSendLog>()
//                              .AsTracking()
//                              .Where(s => s.BusinessId == ev.BusinessId && s.MessageId == ev.ProviderMessageId);

//            var msgLogQ = _db.Set<MessageLog>()
//                             .AsTracking()
//                             .Where(m => m.BusinessId == ev.BusinessId && m.MessageId == ev.ProviderMessageId);

//            // If caller passed a specific CampaignSendLogId, narrow further
//            if (ev.CampaignSendLogId is Guid sid)
//                sendLogQ = sendLogQ.Where(s => s.Id == sid);

//            var sendLog = await sendLogQ.FirstOrDefaultAsync(ct);
//            var msgLog = await msgLogQ.FirstOrDefaultAsync(ct);

//            // 2) Apply transition (idempotent)
//            var changed = ApplyTransition(sendLog, msgLog, ev);

//            // 3) Persist only if something actually changed
//            if (changed > 0)
//                await _db.SaveChangesAsync(ct);
//        }

//        /// <summary>Returns number of entities modified.</summary>
//        private int ApplyTransition(CampaignSendLog? sendLog, MessageLog? msgLog, StatusEvent ev)
//        {
//            int modified = 0;

//            // --- CampaignSendLog updates ---
//            if (sendLog != null)
//            {
//                if (!string.Equals(sendLog.MessageId, ev.ProviderMessageId, StringComparison.Ordinal))
//                {
//                    sendLog.MessageId = ev.ProviderMessageId;
//                    modified++;
//                }

//                switch (ev.State)
//                {
//                    case MessageDeliveryState.Sent:
//                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Sent"))
//                        {
//                            sendLog.SendStatus = "Sent";
//                            modified++;
//                        }
//                        if (sendLog.SentAt == null || sendLog.SentAt == default)
//                            sendLog.SentAt = ev.OccurredAt.UtcDateTime;
//                        break;

//                    case MessageDeliveryState.Delivered:
//                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Read") &&
//                            !EqualsIgnoreCase(sendLog.SendStatus, "Delivered"))
//                        {
//                            sendLog.SendStatus = "Delivered";
//                            modified++;
//                        }
//                        if (sendLog.DeliveredAt == null || sendLog.DeliveredAt == default)
//                            sendLog.DeliveredAt = ev.OccurredAt.UtcDateTime;
//                        break;

//                    case MessageDeliveryState.Read:
//                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Read"))
//                        {
//                            sendLog.SendStatus = "Read";
//                            modified++;
//                        }
//                        if (sendLog.ReadAt == null || sendLog.ReadAt == default)
//                            sendLog.ReadAt = ev.OccurredAt.UtcDateTime;
//                        break;

//                    case MessageDeliveryState.Failed:
//                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Failed"))
//                        {
//                            sendLog.SendStatus = "Failed";
//                            modified++;
//                        }
//                        if (sendLog.ErrorMessage != ev.ErrorMessage)
//                        {
//                            sendLog.ErrorMessage = ev.ErrorMessage;
//                            modified++;
//                        }
//                        break;

//                    case MessageDeliveryState.Deleted:
//                        if (!EqualsIgnoreCase(sendLog.SendStatus, "Deleted"))
//                        {
//                            sendLog.SendStatus = "Deleted";
//                            modified++;
//                        }
//                        break;
//                }
//            }

//            // --- MessageLog updates ---
//            if (msgLog != null)
//            {
//                if (!string.Equals(msgLog.MessageId, ev.ProviderMessageId, StringComparison.Ordinal))
//                {
//                    msgLog.MessageId = ev.ProviderMessageId;
//                    modified++;
//                }

//                switch (ev.State)
//                {
//                    case MessageDeliveryState.Sent:
//                        if (!EqualsIgnoreCase(msgLog.Status, "Sent"))
//                        {
//                            msgLog.Status = "Sent";
//                            modified++;
//                        }
//                        if (msgLog.SentAt == null || msgLog.SentAt == default)
//                            msgLog.SentAt = ev.OccurredAt.UtcDateTime;
//                        break;

//                    case MessageDeliveryState.Delivered:
//                        if (!EqualsIgnoreCase(msgLog.Status, "Read") &&
//                            !EqualsIgnoreCase(msgLog.Status, "Delivered"))
//                        {
//                            msgLog.Status = "Delivered";
//                            modified++;
//                        }
//                        break;

//                    case MessageDeliveryState.Read:
//                        if (!EqualsIgnoreCase(msgLog.Status, "Read"))
//                        {
//                            msgLog.Status = "Read";
//                            modified++;
//                        }
//                        break;

//                    case MessageDeliveryState.Failed:
//                        if (!EqualsIgnoreCase(msgLog.Status, "Failed"))
//                        {
//                            msgLog.Status = "Failed";
//                            modified++;
//                        }
//                        if (msgLog.ErrorMessage != ev.ErrorMessage)
//                        {
//                            msgLog.ErrorMessage = ev.ErrorMessage;
//                            modified++;
//                        }
//                        break;

//                    case MessageDeliveryState.Deleted:
//                        if (!EqualsIgnoreCase(msgLog.Status, "Deleted"))
//                        {
//                            msgLog.Status = "Deleted";
//                            modified++;
//                        }
//                        break;
//                }
//            }

//            if (sendLog == null && msgLog == null)
//            {
//                _log.LogWarning("No matching rows for BusinessId={BusinessId}, MessageId={MessageId}, State={State}",
//                    ev.BusinessId, ev.ProviderMessageId, ev.State);
//            }

//            return modified;
//        }

//        private static bool EqualsIgnoreCase(string? a, string? b) =>
//            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
//    }
//}
