using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Webhooks.Models;

namespace xbytechat.api.Features.Webhooks.Services
{
    public class FailedWebhookLogService : IFailedWebhookLogService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FailedWebhookLogService> _logger;

        public FailedWebhookLogService(AppDbContext context, ILogger<FailedWebhookLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogFailureAsync(FailedWebhookLogDto dto)
        {
            try
            {
                var log = new FailedWebhookLog
                {
                    ErrorMessage = dto.ErrorMessage,
                    SourceModule = dto.SourceModule,
                    FailureType = dto.FailureType,
                    RawJson = dto.RawJson,
                    CreatedAt = dto.CreatedAt
                };

                await _context.FailedWebhookLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to persist webhook error log");
            }
        }

        public async Task<List<FailedWebhookLog>> GetAllAsync()
        {
            return await _context.FailedWebhookLogs
                .OrderByDescending(x => x.CreatedAt)
                .Take(100) // prevent DB overload
                .ToListAsync();
        }

        public async Task<FailedWebhookLog?> GetByIdAsync(Guid id)
        {
            return await _context.FailedWebhookLogs.FindAsync(id);
        }

    }
}
