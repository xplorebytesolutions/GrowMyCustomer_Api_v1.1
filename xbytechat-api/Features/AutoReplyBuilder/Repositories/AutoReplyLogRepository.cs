using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Models;
using xbytechat.api.Shared;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.AutoReplyBuilder.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Repositories
{
    public class AutoReplyLogRepository : IAutoReplyLogRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AutoReplyLogRepository> _logger;

        public AutoReplyLogRepository(AppDbContext context, ILogger<AutoReplyLogRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SaveAsync(AutoReplyLogDto dto)
        {
            try
            {
                var log = new AutoReplyLog
                {
                    Id = dto.Id,
                    BusinessId = dto.BusinessId,
                    ContactId = dto.ContactId,
                    TriggerKeyword = dto.TriggerKeyword,
                    TriggerType = dto.TriggerType,
                    ReplyContent = dto.ReplyContent,
                    TriggeredAt = dto.TriggeredAt,
                    FlowName = dto.FlowName,
                    MessageLogId = dto.MessageLogId
                };

                _context.AutoReplyLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to save AutoReplyLog");
                throw;
            }
        }
    }
}
