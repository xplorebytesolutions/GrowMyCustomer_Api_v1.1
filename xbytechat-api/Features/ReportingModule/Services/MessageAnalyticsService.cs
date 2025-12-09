using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.ReportingModule.DTOs;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.ReportingModule.Services
{
    public class MessageAnalyticsService : IMessageAnalyticsService
    {
        private readonly AppDbContext _context;

        public MessageAnalyticsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<RecentMessageLogDto>> GetRecentLogsAsync(Guid businessId, int limit)
        {
            return await _context.MessageLogs
                .Where(x => x.BusinessId == businessId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RecentMessageLogDto
                {
                    Id = x.Id,
                    RecipientNumber = x.RecipientNumber,
                    MessageContent = x.MessageContent,
                    CreatedAt = x.CreatedAt,
                    CampaignId = x.CampaignId,
                    Status = x.Status,
                    SentAt = x.SentAt,
                })
                .ToListAsync();
        }

        public async Task<PaginatedResponse<RecentMessageLogDto>> GetPaginatedLogsAsync(Guid businessId, PaginatedRequest request)
        {
            var query = _context.MessageLogs
                .Where(x => x.BusinessId == businessId);

            if (!string.IsNullOrEmpty(request.Status))
                query = query.Where(x => x.Status == request.Status);

            if (!string.IsNullOrEmpty(request.Search))
                query = query.Where(x =>
                    x.RecipientNumber.Contains(request.Search) ||
                    x.MessageContent.Contains(request.Search));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new RecentMessageLogDto
                {
                    Id = x.Id,
                    RecipientNumber = x.RecipientNumber,
                    MessageContent = x.MessageContent,
                    CreatedAt = x.CreatedAt,
                    CampaignId = x.CampaignId,
                    Status = x.Status,
                    SentAt = x.SentAt,
                })
                .ToListAsync();

            return new PaginatedResponse<RecentMessageLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

    }
}

