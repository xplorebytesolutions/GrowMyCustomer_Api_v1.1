using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.ReportingModule.DTOs;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.ReportingModule.Services
{
    public interface IMessageAnalyticsService
    {
        Task<List<RecentMessageLogDto>> GetRecentLogsAsync(Guid businessId, int limit);
        Task<PaginatedResponse<RecentMessageLogDto>> GetPaginatedLogsAsync(Guid businessId, PaginatedRequest request);
    }
}

