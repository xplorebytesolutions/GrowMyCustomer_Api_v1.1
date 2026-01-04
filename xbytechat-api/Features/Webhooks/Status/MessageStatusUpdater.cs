// 📄 File: Features/Webhooks/Status/MessageStatusUpdater.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// 👇 adjust if your AppDbContext namespace differs
using xbytechat.api;

using xbytechat.api.Infrastructure.Observability;
using xbytechat_api.Features.Billing.Services;

namespace xbytechat.api.Features.Webhooks.Status
{
    /// <summary>
    /// ✅ Business-scoped status pipeline updater:
    /// - Updates MessageLogs by (BusinessId + ProviderMessageId/MessageId)
    /// - Updates CampaignSendLogs ONLY when it can be targeted safely (by CampaignSendLogId OR BusinessId shadow-property)
    /// - No legacy overloads (by design)
    /// </summary>
    public sealed class MessageStatusUpdater : IMessageStatusUpdater
    {
        private readonly AppDbContext _db;
        private readonly ILogger<MessageStatusUpdater> _log;
        private readonly IBillingIngestService _billing;

        public MessageStatusUpdater(
            AppDbContext db,
            ILogger<MessageStatusUpdater> log,
            IBillingIngestService billing)
        {
            _db = db;
            _log = log;
            _billing = billing;
        }

        public async Task<int> UpdateAsync(StatusEvent ev, CancellationToken ct = default)
        {
            if (ev == null) return 0;

            if (ev.BusinessId == Guid.Empty)
            {
                _log.LogWarning("[StatusUpdater] Skipped: BusinessId is empty.");
                return 0;
            }

            var providerMessageId = (ev.ProviderMessageId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(providerMessageId))
            {
                _log.LogWarning("[StatusUpdater] Skipped: ProviderMessageId is empty. businessId={BusinessId}", ev.BusinessId);
                return 0;
            }

            var normalized = StateToStatusString(ev.State); // sent/delivered/read/failed/deleted
            var tsUtc = ev.OccurredAt.UtcDateTime;
            var error = ev.ErrorMessage;

            var affectedCampaign = await TryUpdateCampaignSendLogsSafelyAsync(
                businessId: ev.BusinessId,
                campaignSendLogId: ev.CampaignSendLogId,
                messageId: providerMessageId,
                normalizedStatus: normalized,
                tsUtc: tsUtc,
                error: error,
                ct: ct
            );

            var affectedMessages = await UpdateMessageLogsAsync(
                businessId: ev.BusinessId,
                messageId: providerMessageId,
                normalizedStatus: normalized,
                tsUtc: tsUtc,
                error: error,
                ct: ct
            );

            if (affectedCampaign == 0 && affectedMessages == 0)
            {
                _log.LogWarning(
                    "[StatusUpdater] No rows updated. businessId={BusinessId}, messageId={MessageId}, status={Status}",
                    ev.BusinessId,
                    providerMessageId,
                    normalized
                );
            }
            else
            {
                if (normalized == "failed") MetricsRegistry.MessagesFailed.Add(1);
                else if (normalized == "sent") MetricsRegistry.MessagesSent.Add(1);
            }

            return affectedCampaign + affectedMessages;
        }

        private async Task<int> UpdateMessageLogsAsync(
            Guid businessId,
            string messageId,
            string normalizedStatus,
            DateTime tsUtc,
            string? error,
            CancellationToken ct)
        {
            // ✅ STRICT business scope + ✅ OUTGOING ONLY (Blocker #1 fix)
            var mlQuery = _db.MessageLogs
                .Where(m => m.BusinessId == businessId && !m.IsIncoming)
                .Where(m => m.ProviderMessageId == messageId || m.MessageId == messageId);

            // Avoid downgrades:
            // - delivered must not overwrite read
            // - sent should only move from Queued/empty
            // - failed should not overwrite delivered/read (keeps UI sane)

            if (normalizedStatus == "delivered")
            {
                return await mlQuery
                    .Where(m => m.Status != "Read" && m.Status != "read")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "Delivered")
                        .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }

            if (normalizedStatus == "read")
            {
                return await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Read")
                    .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }

            if (normalizedStatus == "failed")
            {
                return await mlQuery
                    .Where(m =>
                        m.Status != "Delivered" && m.Status != "delivered" &&
                        m.Status != "Read" && m.Status != "read")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "Failed")
                        .SetProperty(m => m.ErrorMessage, error)
                        .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }

            if (normalizedStatus == "sent")
            {
                return await mlQuery
                    .Where(m =>
                        m.Status == null ||
                        m.Status == "" ||
                        m.Status == "Queued" ||
                        m.Status == "queued")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "Sent")
                        .SetProperty(m => m.SentAt, m => m.SentAt ?? tsUtc), ct);
            }

            if (normalizedStatus == "deleted")
            {
                return await mlQuery.ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, "Deleted")
                    .SetProperty(m => m.ErrorMessage, error), ct);
            }

            return 0;
        }

        private async Task<int> TryUpdateCampaignSendLogsSafelyAsync(
            Guid businessId,
            Guid? campaignSendLogId,
            string messageId,
            string normalizedStatus,
            DateTime tsUtc,
            string? error,
            CancellationToken ct)
        {
            try
            {
                var csl = _db.CampaignSendLogs.AsQueryable();

                if (campaignSendLogId.HasValue && campaignSendLogId.Value != Guid.Empty)
                {
                    csl = csl.Where(x => x.Id == campaignSendLogId.Value);
                }
                else
                {
                    // ✅ STRICT business scope using shadow property
                    csl = csl.Where(x =>
                        EF.Property<Guid>(x, "BusinessId") == businessId &&
                        x.MessageId == messageId
                    );
                }

                if (normalizedStatus == "delivered")
                {
                    return await csl.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ErrorMessage, error)
                        .SetProperty(x => x.DeliveredAt, tsUtc)
                        .SetProperty(x => x.SendStatus, "Delivered")
                        .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc), ct);
                }

                if (normalizedStatus == "read")
                {
                    return await csl.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ErrorMessage, error)
                        .SetProperty(x => x.ReadAt, tsUtc)
                        .SetProperty(x => x.SendStatus, "Read")
                        .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc)
                        .SetProperty(x => x.DeliveredAt, x => x.DeliveredAt ?? tsUtc), ct);
                }

                if (normalizedStatus == "failed")
                {
                    return await csl.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ErrorMessage, error)
                        .SetProperty(x => x.SendStatus, "Failed")
                        .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc), ct);
                }

                if (normalizedStatus == "sent")
                {
                    return await csl.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ErrorMessage, error)
                        .SetProperty(x => x.SentAt, x => x.SentAt ?? tsUtc)
                        .SetProperty(x => x.SendStatus, "Sent"), ct);
                }

                if (normalizedStatus == "deleted")
                {
                    return await csl.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ErrorMessage, error)
                        .SetProperty(x => x.SendStatus, "Deleted"), ct);
                }

                return await csl.ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ErrorMessage, error), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    ex,
                    "[StatusUpdater] CampaignSendLogs update skipped to preserve tenant safety. businessId={BusinessId}, messageId={MessageId}",
                    businessId,
                    messageId
                );
                return 0;
            }
        }

        private static string StateToStatusString(MessageDeliveryState state) => state switch
        {
            MessageDeliveryState.Sent => "sent",
            MessageDeliveryState.Delivered => "delivered",
            MessageDeliveryState.Read => "read",
            MessageDeliveryState.Failed => "failed",
            MessageDeliveryState.Deleted => "deleted",
            _ => "unknown"
        };
    }
}
