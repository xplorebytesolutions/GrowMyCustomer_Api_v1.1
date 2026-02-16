using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.MessageManagement.Services;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Models.BusinessModel;
using xbytechat.api.AuthModule.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace xbytechat.api.Features.MessageManagement.Services
{
    public class MessageStatusService : IMessageStatusService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MessageStatusService> _logger;

        public MessageStatusService(AppDbContext context, ILogger<MessageStatusService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogWebhookStatusAsync(WebhookStatusDto dto)
        {
            foreach (var status in dto.statuses)
            {
                var messageId = string.IsNullOrWhiteSpace(status.id) ? null : status.id.Trim();
                var normalizedStatus = (status.status ?? string.Empty).Trim().ToLowerInvariant();
                var metaTimestamp = status.timestamp;

                if (!string.IsNullOrWhiteSpace(messageId))
                {
                    var exists = await _context.MessageStatusLogs.AsNoTracking().AnyAsync(x =>
                        x.MessageId == messageId &&
                        x.Status == normalizedStatus &&
                        x.MetaTimestamp == metaTimestamp);

                    if (exists) continue;
                }

                var log = new MessageStatusLog
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    Status = normalizedStatus,
                    RecipientNumber = status.recipient_id,
                    MetaTimestamp = metaTimestamp,
                    TemplateCategory = status?.pricing?.category,
                    MessageType = status?.conversation?.origin?.type ?? "session",
                    Channel = "whatsapp",
                    CreatedAt = DateTime.UtcNow,
                    RawPayload = System.Text.Json.JsonSerializer.Serialize(status)
                };

                var statusTime = DateTimeOffset.FromUnixTimeSeconds(status.timestamp).UtcDateTime;

                switch (status.status.ToLower())
                {
                    case "sent": log.SentAt = statusTime; break;
                    case "delivered": log.DeliveredAt = statusTime; break;
                    case "read": log.ReadAt = statusTime; break;
                }

                if (status.errors != null && status.errors.Count > 0)
                {
                    log.ErrorMessage = status.errors[0].details;
                    log.ErrorCode = status.errors[0].code;
                }

                await _context.MessageStatusLogs.AddAsync(log);
            }

            // ⛑️ Wrap in try-catch and log full inner exception
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                _logger.LogInformation("↩️ Duplicate status webhook rows ignored by DB idempotency guard.");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ SaveChangesAsync failed: " + ex.Message);
                _logger.LogError("❌ Inner exception: " + ex.InnerException?.Message);
                throw;
            }
        }

    }
}
