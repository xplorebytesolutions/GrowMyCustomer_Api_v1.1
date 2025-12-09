using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CampaignTracking.Models; // CampaignSendLog (optional if you keep only MessageLogs lookup)
using xbytechat.api.Features.MessageManagement.Services; // IMessageStatusUpdater
using xbytechat.api.Features.Webhooks.Status;
using xbytechat.api.Infrastructure; // your AppDbContext namespace (adjust if different)
using xbytechat.api.Infrastructure.Observability; // MetricsRegistry

namespace xbytechat.api.Features.Webhooks.Services
{
    

    public sealed class WhatsAppWebhookService : IWhatsAppWebhookService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WhatsAppWebhookService> _logger;
        private readonly IMessageStatusUpdater _updater;

        public WhatsAppWebhookService(
            AppDbContext context,
            ILogger<WhatsAppWebhookService> logger,
            IMessageStatusUpdater updater)
        {
            _context = context;
            _logger = logger;
            _updater = updater;
        }

        /// <summary>
        /// Single modern entrypoint. Assumes payload is Meta-shaped (entry[].changes[].value.statuses[]).
        /// If you ingest other providers, adapt them to this shape before calling here.
        /// </summary>
        public async Task ProcessStatusUpdateAsync(Guid businessId, string provider, JsonElement payload, CancellationToken ct = default)
        {
            provider = (provider ?? "").Trim().ToLowerInvariant();

            // Normalize status text to what the updater expects
            static string NormalizeStatus(string? s)
            {
                s = (s ?? "").Trim().ToLowerInvariant();
                return s switch
                {
                    "sent" => "sent",
                    "delivered" => "delivered",
                    "read" => "read",
                    "failed" => "failed",
                    _ => s
                };
            }

            // Parse Meta-like envelope: entry[].changes[].value.statuses[]
            if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Status payload missing 'entry' array.");
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

                    foreach (var st in statuses.EnumerateArray())
                    {
                        // Fields: id (WAMID or provider id), status, timestamp
                        string? providerOrWaId = st.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        string? statusText = st.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                        long unixTs = 0;
                        if (st.TryGetProperty("timestamp", out var tsProp))
                        {
                            if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var parsed))
                                unixTs = parsed;
                            else if (tsProp.ValueKind == JsonValueKind.Number)
                                unixTs = tsProp.GetInt64();
                        }

                        if (string.IsNullOrWhiteSpace(providerOrWaId) || string.IsNullOrWhiteSpace(statusText))
                        {
                            _logger.LogWarning("Status item missing id or status: {Item}", st.GetRawText());
                            continue;
                        }

                        // Resolve canonical MessageId (WAMID) from DB; fallback to provider id if not found.
                        // 1) Try MessageLogs
                        string? messageId = await _context.MessageLogs.AsNoTracking()
                            .Where(m => m.ProviderMessageId == providerOrWaId || m.MessageId == providerOrWaId)
                            .OrderByDescending(m => m.CreatedAt)
                            .Select(m => m.MessageId ?? m.ProviderMessageId)
                            .FirstOrDefaultAsync(ct);

                        // 2) Optional: also look in CampaignSendLogs if desired
                        if (string.IsNullOrWhiteSpace(messageId))
                        {
                            messageId = await _context.CampaignSendLogs.AsNoTracking()
                                .Where(c => c.MessageId == providerOrWaId)
                                .OrderByDescending(c => c.CreatedAt)
                                .Select(c => c.MessageId)
                                .FirstOrDefaultAsync(ct);
                        }

                        messageId ??= providerOrWaId;

                        var tsUtc = unixTs > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime
                            : DateTime.UtcNow;

                        var norm = NormalizeStatus(statusText);

                        // Updater signature: (messageId, status, tsUtc, error, ct)
                        await _updater.UpdateAsync(messageId, norm, tsUtc, null, ct);

                        _logger.LogInformation("Status updated: msg={MessageId} status={Status} ts={Ts}", messageId, norm, tsUtc);
                    }
                }
            }
        }
    }
}
