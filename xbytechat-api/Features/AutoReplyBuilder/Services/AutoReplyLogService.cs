using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    /// <summary>
    /// Query-only service for reading AutoReplyLogs for analytics / UI.
    /// </summary>
    public sealed class AutoReplyLogService : IAutoReplyLogService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AutoReplyLogService> _logger;

        public AutoReplyLogService(
            AppDbContext dbContext,
            ILogger<AutoReplyLogService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IReadOnlyList<AutoReplyLogSummaryDto>> GetRecentAsync(
            Guid businessId,
            int take,
            CancellationToken cancellationToken = default)
        {
            if (businessId == Guid.Empty)
            {
                return Array.Empty<AutoReplyLogSummaryDto>();
            }

            // Clamp "take" to safe range
            if (take <= 0) take = 20;
            if (take > 100) take = 100;

            _logger.LogDebug(
                "Fetching {Take} recent AutoReplyLogs for BusinessId={BusinessId}",
                take, businessId);

            var query = _dbContext.Set<AutoReplyLog>()
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId)
                .OrderByDescending(x => x.TriggeredAt)
                .Take(take);

            var items = await query
                .Select(x => new AutoReplyLogSummaryDto
                {
                    Id = x.Id,
                    TriggerType = x.TriggerType,
                    TriggerKeyword = x.TriggerKeyword,
                    FlowName = x.FlowName,
                    ReplyContent = x.ReplyContent,
                    ContactId = x.ContactId,
                    MessageLogId = x.MessageLogId,
                    TriggeredAt = x.TriggeredAt
                })
                .ToListAsync(cancellationToken);

            return items;
        }
    }
}


//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using xbytechat.api;
//using xbytechat.api.Features.AutoReplyBuilder.DTOs;
//using xbytechat.api.Features.AutoReplyBuilder.Models;

//namespace xbytechat.api.Features.AutoReplyBuilder.Services
//{
//    /// <summary>
//    /// Query-only service for reading AutoReplyLogs for analytics / UI.
//    /// </summary>
//    public sealed class AutoReplyLogService : IAutoReplyLogService
//    {
//        private readonly AppDbContext _dbContext;
//        private readonly ILogger<AutoReplyLogService> _logger;

//        public AutoReplyLogService(
//            AppDbContext dbContext,
//            ILogger<AutoReplyLogService> logger)
//        {
//            _dbContext = dbContext;
//            _logger = logger;
//        }

//        public async Task<IReadOnlyList<AutoReplyLogSummaryDto>> GetRecentAsync(
//            Guid businessId,
//            int take,
//            CancellationToken cancellationToken = default)
//        {
//            if (businessId == Guid.Empty)
//            {
//                return Array.Empty<AutoReplyLogSummaryDto>();
//            }

//            // clamp "take" to a safe range
//            if (take <= 0) take = 20;
//            if (take > 100) take = 100;

//            _logger.LogDebug(
//                "Fetching {Take} recent AutoReplyLogs for BusinessId={BusinessId}",
//                take, businessId);

//            var query = _dbContext.Set<AutoReplyLog>()
//                .AsNoTracking()
//                .Where(x => x.BusinessId == businessId)
//                .OrderByDescending(x => x.TriggeredAt)
//                .Take(take);

//            var items = await query
//                .Select(x => new AutoReplyLogSummaryDto
//                {
//                    Id = x.Id,
//                    TriggerType = x.TriggerType,
//                    TriggerKeyword = x.TriggerKeyword,
//                    FlowName = x.FlowName,
//                    ReplyContent = x.ReplyContent,
//                    ContactId = x.ContactId,
//                    MessageLogId = x.MessageLogId,
//                    TriggeredAt = x.TriggeredAt
//                })
//                .ToListAsync(cancellationToken);

//            return items;
//        }
//    }
//}
