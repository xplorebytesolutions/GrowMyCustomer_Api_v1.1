// 📄 File: Features/Webhooks/Services/WhatsAppWebhookService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Inbox.Hubs;
using xbytechat.api.Features.Webhooks.Status;
using xbytechat.api.Infrastructure; // AppDbContext (keep as-is if correct)

namespace xbytechat.api.Features.Webhooks.Services
{
    public sealed class WhatsAppWebhookService : IWhatsAppWebhookService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WhatsAppWebhookService> _logger;
        private readonly IMessageStatusUpdater _updater;
        private readonly IHubContext<InboxHub> _hubContext;

        public WhatsAppWebhookService(
            AppDbContext context,
            ILogger<WhatsAppWebhookService> logger,
            IMessageStatusUpdater updater,
            IHubContext<InboxHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _updater = updater;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Single modern entrypoint. Assumes payload is Meta-shaped (entry[].changes[].value.statuses[]).
        /// BusinessId MUST be resolved by the caller (ProviderDirectory or equivalent).
        /// </summary>
        public async Task ProcessStatusUpdateAsync(Guid businessId, string provider, JsonElement payload, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
            {
                _logger.LogWarning("Status update skipped: businessId is empty.");
                return;
            }

            provider = (provider ?? string.Empty).Trim();

            static string NormalizeStatus(string? s)
            {
                s = (s ?? "").Trim().ToLowerInvariant();
                return s switch
                {
                    "sent" => "sent",
                    "delivered" => "delivered",
                    "read" => "read",
                    "failed" => "failed",
                    "deleted" => "deleted",
                    _ => s
                };
            }

            static DateTime ParseTimestampUtc(JsonElement statusItem)
            {
                long unixTs = 0;

                if (statusItem.TryGetProperty("timestamp", out var tsProp))
                {
                    if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var parsed))
                        unixTs = parsed;
                    else if (tsProp.ValueKind == JsonValueKind.Number)
                        unixTs = tsProp.GetInt64();
                }

                return unixTs > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime
                    : DateTime.UtcNow;
            }

            static string? TryExtractError(JsonElement statusItem)
            {
                // Meta shape often: errors: [{ code, title, details, error_data... }]
                if (!statusItem.TryGetProperty("errors", out var errorsEl) || errorsEl.ValueKind != JsonValueKind.Array)
                    return null;

                if (errorsEl.GetArrayLength() == 0)
                    return null;

                var e0 = errorsEl[0];

                string? title = e0.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
                string? details = e0.TryGetProperty("details", out var detailsEl) ? detailsEl.GetString() : null;

                title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
                details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();

                if (title == null && details == null) return null;

                return title != null && details != null
                    ? $"{title} - {details}"
                    : (title ?? details);
            }

            // Parse Meta-like envelope: entry[].changes[].value.statuses[]
            if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Status payload missing 'entry' array. businessId={BusinessId}", businessId);
                return;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!value.TryGetProperty("statuses", out var statuses) || statuses.ValueKind != JsonValueKind.Array)
                        continue;

                    // ✅ Performance: batch DB lookup for all ids in this statuses[] block
                    var providerIds = new List<string>(capacity: Math.Max(4, statuses.GetArrayLength()));
                    foreach (var st in statuses.EnumerateArray())
                    {
                        var id = st.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(id))
                            providerIds.Add(id!.Trim());
                    }

                    if (providerIds.Count == 0)
                        continue;

                    // ✅ Business-scoped lookup ONLY (prevents cross-tenant updates)
                    // We match both ProviderMessageId and MessageId to support older rows.
                    var hits = await _context.MessageLogs
                        .AsNoTracking()
                        .Where(m => m.BusinessId == businessId &&
                                    (
                                        (m.ProviderMessageId != null && providerIds.Contains(m.ProviderMessageId)) ||
                                        (m.MessageId != null && providerIds.Contains(m.MessageId))
                                    ))
                        .Select(m => new
                        {
                            m.Id,
                            m.ContactId,
                            m.ProviderMessageId,
                            m.MessageId,
                            m.CreatedAt
                        })
                        .ToListAsync(ct);

                    // Map: providerId -> best match row
                    // If both ProviderMessageId and MessageId collide, we pick the newest CreatedAt.
                    var map = new Dictionary<string, (Guid msgLogId, Guid? contactId, string canonicalMessageId, DateTime createdAt)>(StringComparer.Ordinal);
                    foreach (var h in hits.OrderByDescending(x => x.CreatedAt))
                    {
                        var canonical = (h.MessageId ?? h.ProviderMessageId ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(canonical)) continue;

                        if (!string.IsNullOrWhiteSpace(h.ProviderMessageId))
                        {
                            var key = h.ProviderMessageId.Trim();
                            if (!map.ContainsKey(key))
                                map[key] = (h.Id, h.ContactId, canonical, h.CreatedAt);
                        }

                        if (!string.IsNullOrWhiteSpace(h.MessageId))
                        {
                            var key = h.MessageId.Trim();
                            if (!map.ContainsKey(key))
                                map[key] = (h.Id, h.ContactId, canonical, h.CreatedAt);
                        }
                    }

                    foreach (var st in statuses.EnumerateArray())
                    {
                        string? providerMessageId = st.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        string? statusText = st.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                        providerMessageId = string.IsNullOrWhiteSpace(providerMessageId) ? null : providerMessageId.Trim();
                        statusText = string.IsNullOrWhiteSpace(statusText) ? null : statusText.Trim();

                        if (providerMessageId == null || statusText == null)
                        {
                            _logger.LogWarning("Status item missing id or status. businessId={BusinessId}, item={Item}",
                                businessId, st.GetRawText());
                            continue;
                        }

                        if (!map.TryGetValue(providerMessageId, out var row))
                        {
                            // ✅ Critical rule: if not found within THIS business → skip
                            _logger.LogWarning(
                                "Status update skipped (no business-scoped match). businessId={BusinessId}, providerMessageId={ProviderMessageId}, status={Status}",
                                businessId,
                                providerMessageId,
                                statusText
                            );
                            continue;
                        }

                        var norm = NormalizeStatus(statusText);
                        var tsUtc = ParseTimestampUtc(st);
                        var err = TryExtractError(st);

                        // ✅ Industry-grade call: business-scoped updater (prevents cross-tenant updates)
                        var ev = new StatusEvent
                        {
                            BusinessId = businessId,
                            Provider = provider,
                            ProviderMessageId = providerMessageId,
                            State = norm switch
                            {
                                "sent" => MessageDeliveryState.Sent,
                                "delivered" => MessageDeliveryState.Delivered,
                                "read" => MessageDeliveryState.Read,
                                "failed" => MessageDeliveryState.Failed,
                                "deleted" => MessageDeliveryState.Deleted,
                                _ => MessageDeliveryState.Sent // safe fallback; updater also guards downgrades
                            },
                            OccurredAt = new DateTimeOffset(tsUtc),
                            ErrorMessage = err
                        };

                        await _updater.UpdateAsync(ev, ct);

                        // SignalR: notify only the correct business group
                        var groupName = $"business_{businessId}";
                        await _hubContext.Clients.Group(groupName).SendAsync("MessageStatusChanged", new
                        {
                            businessId,
                            provider,
                            providerMessageId = providerMessageId,
                            messageId = row.canonicalMessageId, // canonical id we stored
                            messageLogId = row.msgLogId,
                            contactId = row.contactId,
                            status = norm,
                            occurredAtUtc = tsUtc,
                            error = err
                        }, ct);

                        _logger.LogInformation(
                            "✅ Status updated (business-scoped): businessId={BusinessId}, providerMessageId={ProviderMessageId}, status={Status}, ts={TsUtc}",
                            businessId, providerMessageId, norm, tsUtc
                        );
                    }
                }
            }
        }
    }
}
