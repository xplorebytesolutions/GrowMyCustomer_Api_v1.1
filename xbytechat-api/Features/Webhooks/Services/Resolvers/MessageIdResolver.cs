using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;
using xbytechat.api;

namespace xbytechat.api.Features.Webhooks.Services.Resolvers
{
    public class MessageIdResolver : IMessageIdResolver
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MessageIdResolver> _logger;

        public MessageIdResolver(AppDbContext context, ILogger<MessageIdResolver> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<string?> ResolveAsync(string providerMessageId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(providerMessageId))
                return null;

            // 1) Already a WAMID? Return as-is.
            if (providerMessageId.StartsWith("wamid.", StringComparison.OrdinalIgnoreCase))
                return providerMessageId;

            // 2) Try MessageLogs mapping (most reliable)
            //    We pick any field that looks like a WAMID if present; otherwise fall back to MessageId.
            var mlHit = await _context.MessageLogs.AsNoTracking()
                .Where(m => m.ProviderMessageId == providerMessageId || m.MessageId == providerMessageId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m =>
                    m.ProviderMessageId.StartsWith("wamid.", StringComparison.OrdinalIgnoreCase)
                        ? m.ProviderMessageId
                        : (m.MessageId ?? m.ProviderMessageId))
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrEmpty(mlHit))
                return mlHit;

            // 3) Some paths write WAMID straight into CampaignSendLogs.MessageId (no mapping required)
            var cslHit = await _context.CampaignSendLogs.AsNoTracking()
                .Where(c => c.MessageId == providerMessageId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => c.MessageId)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrEmpty(cslHit))
                return cslHit;

            // 4) Fallback: return original (keeps pipeline flowing even if we can’t map)
            _logger.LogDebug("MessageIdResolver: passthrough for provider id {ProviderMessageId}", providerMessageId);
            return providerMessageId;
        }

        public async Task<Guid?> ResolveCampaignSendLogIdAsync(string messageId)
        {
            var log = await _context.CampaignSendLogs
                                .FirstOrDefaultAsync(l => l.MessageId == messageId);

            if (log == null)
            {
                _logger.LogWarning("⚠️ CampaignSendLog not found for MessageId: {MessageId}", messageId);
                return null;
            }

            return log.Id;
        }

        public async Task<Guid?> ResolveMessageLogIdAsync(string messageId)
        {
            var log = await _context.MessageLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.MessageId == messageId);

            if (log == null)
            {
                _logger.LogWarning("⚠️ MessageLog not found for MessageId: {MessageId}", messageId);
                return null;
            }

            return log.Id;
        }

        public async Task<Guid?> ResolveBusinessIdByMessageIdAsync(string messageId)
        {
            var log = await _context.MessageLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.MessageId == messageId);

            if (log == null)
            {
                _logger.LogWarning("⚠️ MessageLog not found for MessageId: {MessageId}", messageId);
                return null;
            }

            return log.BusinessId;
        }

    }
}
