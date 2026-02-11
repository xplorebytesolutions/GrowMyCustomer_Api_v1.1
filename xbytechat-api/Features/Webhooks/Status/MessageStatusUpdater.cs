// 📄 File: Features/Webhooks/Status/MessageStatusUpdater.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using xbytechat.api;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Infrastructure.Observability;
using xbytechat_api.Features.Billing.Services;


namespace xbytechat.api.Features.Webhooks.Status
{
    /// <summary>
    /// ✅ Business-scoped status pipeline updater:
    /// - Updates MessageLogs by (BusinessId + ProviderMessageId/MessageId)
    /// - Updates CampaignSendLogs ONLY when it can be targeted safely (by CampaignSendLogId OR BusinessId shadow-property)
    /// - No legacy overloads (by design)
    /// 
    /// Phase 5 (Feedback loop):
    /// - On failed deliveries, classify errors and update Contact OptStatus/ChannelStatus
    ///   so outbound consent guard blocks future sends deterministically.
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
            var errorCode = TryGetErrorCode(ev);

            // Keep existing campaign + message log updates intact
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

            if (normalized == "failed")
            {
                // Compliance feedback loop: failed delivery signals may update contact health
                // so future outbound sends are blocked by the centralized guard.
                await TryUpdateContactHealthOnFailureAsync(
                    businessId: ev.BusinessId,
                    messageId: providerMessageId,
                    errorCode: errorCode,
                    errorMessage: error,
                    ev: ev,
                    ct: ct
                );
            }

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
            // ✅ STRICT business scope + ✅ OUTGOING ONLY
            var mlQuery = _db.MessageLogs
                .Where(m => m.BusinessId == businessId && !m.IsIncoming)
                .Where(m => m.ProviderMessageId == messageId || m.MessageId == messageId);

            // Avoid downgrades:
            // - delivered must not overwrite read
            // - sent should only move from Queued/empty
            // - failed should not overwrite delivered/read

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

        // Conservative classifier: update contact state only on high-confidence failure signals.
        private static (bool optedOut, bool invalidNumber, string? reason) ClassifyFailure(string? errorCode, string? errorMessage)
        {
            var code = (errorCode ?? string.Empty).Trim().ToUpperInvariant();
            var msg = (errorMessage ?? string.Empty).Trim().ToUpperInvariant();

            // Strong evidence: recipient blocked or explicit provider-side opt-out.
            if (ContainsAny(msg,
                    "BLOCKED BY USER", "RECIPIENT BLOCKED", "USER BLOCKED", "HAS BLOCKED", "RECIPIENT_HAS_BLOCKED",
                    "OPTED OUT", "UNSUBSCRIBE", "UNSUBSCRIBED", "USER REQUESTED TO STOP") ||
                ContainsAny(code, "BLOCKED", "OPT_OUT", "UNSUBSCRIBE"))
            {
                return (true, false, "ProviderBlocked");
            }

            // Strong evidence: number is invalid or not a WhatsApp account.
            if (ContainsAny(msg,
                    "NOT A WHATSAPP USER", "DOES NOT EXIST", "INVALID PHONE", "INVALID NUMBER",
                    "PHONE NUMBER IS NOT VALID", "NO WHATSAPP ACCOUNT") ||
                ContainsAny(code, "INVALID_NUMBER", "NOT_WHATSAPP_USER", "NUMBER_DOES_NOT_EXIST"))
            {
                return (false, true, null);
            }

            return (false, false, null);
        }

        private async Task TryUpdateContactHealthOnFailureAsync(
            Guid businessId,
            string messageId,
            string? errorCode,
            string? errorMessage,
            StatusEvent ev,
            CancellationToken ct)
        {
            var classification = ClassifyFailure(errorCode, errorMessage);
            if (!classification.optedOut && !classification.invalidNumber) return;

            try
            {
                // Prefer MessageLog context first (contact id + recipient are already in this updater's data path).
                var messageContext = await _db.MessageLogs
                    .AsNoTracking()
                    .Where(m => m.BusinessId == businessId && !m.IsIncoming)
                    .Where(m => m.ProviderMessageId == messageId || m.MessageId == messageId)
                    .Select(m => new { m.ContactId, m.RecipientNumber })
                    .FirstOrDefaultAsync(ct);

                Guid? contactId = messageContext?.ContactId;
                string? recipientPhone = messageContext?.RecipientNumber;

                // Fallback recipient extraction from StatusEvent only if needed.
                recipientPhone ??= TryGetEventRecipient(ev);
                var lookupCandidates = BuildPhoneLookupCandidates(recipientPhone);

                Contact? contact = null;

                if (contactId.HasValue && contactId.Value != Guid.Empty)
                {
                    contact = await _db.Contacts.FirstOrDefaultAsync(
                        c => c.BusinessId == businessId && c.Id == contactId.Value,
                        ct);
                }

                if (contact == null && lookupCandidates.Count > 0)
                {
                    contact = await _db.Contacts.FirstOrDefaultAsync(
                        c => c.BusinessId == businessId && lookupCandidates.Contains(c.PhoneNumber),
                        ct);
                }

                if (contact == null)
                {
                    _log.LogWarning(
                        "[StatusUpdater] Failure classified but contact not found. businessId={BusinessId}, messageId={MessageId}",
                        businessId,
                        messageId);
                    return;
                }

                var nowUtc = DateTime.UtcNow;
                var changed = false;

                // Idempotency guard: update timestamps only when state actually changes.
                if (classification.invalidNumber && contact.ChannelStatus != ContactChannelStatus.InvalidNumber)
                {
                    contact.ChannelStatus = ContactChannelStatus.InvalidNumber;
                    contact.ChannelStatusUpdatedAt = nowUtc;
                    changed = true;
                }

                // Only set opted-out on explicit high-confidence opt-out signals.
                if (classification.optedOut && contact.OptStatus != ContactOptStatus.OptedOut)
                {
                    contact.OptStatus = ContactOptStatus.OptedOut;
                    contact.OptStatusUpdatedAt = nowUtc;
                    contact.OptOutReason = "ProviderBlocked";
                    changed = true;
                }

                if (!changed) return;

                await _db.SaveChangesAsync(ct);

                _log.LogInformation(
                    "[StatusUpdater] Contact health updated from failed status. businessId={BusinessId}, contactId={ContactId}, phone={Phone}, opt={OptStatus}, channel={ChannelStatus}",
                    businessId,
                    contact.Id,
                    contact.PhoneNumber,
                    contact.OptStatus,
                    contact.ChannelStatus);
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    ex,
                    "[StatusUpdater] Contact health update skipped after failed status. businessId={BusinessId}, messageId={MessageId}",
                    businessId,
                    messageId);
            }
        }

        private static bool ContainsAny(string source, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;
            return patterns.Any(p => source.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeDigitsOnly(string? value)
            => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static string NormalizePhoneForLookup(string? value)
        {
            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var normalized = PhoneNumberNormalizer.NormalizeToE164Digits(raw, "IN");
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;

            var digits = NormalizeDigitsOnly(raw);
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            normalized = PhoneNumberNormalizer.NormalizeToE164Digits("+" + digits, "IN");
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;

            return digits;
        }

        private static List<string> BuildPhoneLookupCandidates(string? value)
        {
            var raw = (value ?? string.Empty).Trim();
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            var normalized = NormalizePhoneForLookup(raw);
            var digits = NormalizeDigitsOnly(raw);

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
                candidates.Add("+" + normalized);

                if (normalized.Length == 12 && normalized.StartsWith("91", StringComparison.Ordinal))
                    candidates.Add(normalized.Substring(2));
            }

            if (!string.IsNullOrWhiteSpace(digits))
            {
                candidates.Add(digits);
                candidates.Add("+" + digits);

                if (digits.Length == 10)
                {
                    candidates.Add("91" + digits);
                    candidates.Add("+91" + digits);
                }
                else if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
                {
                    candidates.Add(digits.Substring(2));
                }
            }

            if (!string.IsNullOrWhiteSpace(raw))
                candidates.Add(raw);

            return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private static string? TryGetErrorCode(StatusEvent ev)
        {
            // Keep this reflection-based to avoid coupling to provider-specific event shapes.
            var type = ev.GetType();
            var value =
                type.GetProperty("ErrorCode")?.GetValue(ev)?.ToString() ??
                type.GetProperty("ProviderErrorCode")?.GetValue(ev)?.ToString() ??
                type.GetProperty("Code")?.GetValue(ev)?.ToString();

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? TryGetEventRecipient(StatusEvent ev)
        {
            // Use only if message/campaign linkage cannot provide recipient resolution.
            var type = ev.GetType();
            var value =
                type.GetProperty("RecipientNumber")?.GetValue(ev)?.ToString() ??
                type.GetProperty("To")?.GetValue(ev)?.ToString() ??
                type.GetProperty("PhoneNumber")?.GetValue(ev)?.ToString();

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
