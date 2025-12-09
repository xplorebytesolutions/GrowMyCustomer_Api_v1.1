using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignTracking.DTOs;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CampaignTracking.Services;
using xbytechat.api.Features.CRM.Dtos;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public class CampaignSendLogService : ICampaignSendLogService
    {
        private readonly AppDbContext _context;
        private readonly ICampaignSendLogEnricher _enricher;


        public CampaignSendLogService(AppDbContext context, ICampaignSendLogEnricher enricher)
        {
            _context = context;
            _enricher = enricher;
        }

        //public async Task<PagedResult<CampaignSendLogDto>> GetLogsByCampaignIdAsync(
        //Guid campaignId, string? status, string? search, int page, int pageSize)
        //{
        //    // Base query as IQueryable (not IIncludableQueryable)
        //    IQueryable<CampaignSendLog> query = _context.CampaignSendLogs
        //        .AsNoTracking()
        //        .Where(log => log.CampaignId.HasValue && log.CampaignId.Value == campaignId);

        //    if (!string.IsNullOrEmpty(status))
        //        query = query.Where(log => log.SendStatus == status);

        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        var keyword = search.ToLower();

        //        // Safe null checks + server-side case-insensitive matching
        //        // If you're on PostgreSQL, EF.Functions.ILike is nicer; otherwise use ToLower().
        //        query = query.Where(log =>
        //            log.Contact != null &&
        //           (log.Contact.Name.ToLower().Contains(keyword)
        //         || log.Contact.PhoneNumber.Contains(keyword)));
        //    }

        //    // Add Include after filters to keep it IQueryable
        //    query = query.Include(log => log.Contact);

        //    var totalCount = await query.CountAsync();

        //    var logs = await query
        //        .OrderByDescending(log => log.CreatedAt)
        //        .Skip((page - 1) * pageSize)
        //        .Take(pageSize)
        //        .Select(log => new CampaignSendLogDto
        //        {
        //            Id = log.Id,
        //            CampaignId = log.CampaignId!.Value,       // unwrap once for DTO Guid
        //            ContactId = log.ContactId,               // keep Guid? in DTO if CSV-only is allowed
        //            ContactName = log.Contact != null ? log.Contact.Name : "N/A",
        //            ContactPhone = log.Contact != null ? log.Contact.PhoneNumber : "-",
        //            MessageBody = log.MessageBody,
        //            TemplateId = log.TemplateId,
        //            SendStatus = log.SendStatus,
        //            ErrorMessage = log.ErrorMessage,
        //            CreatedAt = log.CreatedAt,
        //            SentAt = log.SentAt,
        //            DeliveredAt = log.DeliveredAt,
        //            ReadAt = log.ReadAt,
        //            SourceChannel = log.SourceChannel,
        //            IsClicked = log.IsClicked,
        //            ClickedAt = log.ClickedAt,
        //            ClickType = log.ClickType
        //        })
        //        .ToListAsync();

        //    return new PagedResult<CampaignSendLogDto>
        //    {
        //        Items = logs,
        //        TotalCount = totalCount,
        //        Page = page,
        //        PageSize = pageSize
        //    };
        //}


        //public async Task<List<CampaignSendLogDto>> GetLogsForContactAsync(Guid campaignId, Guid contactId)
        //{
        //    return await _context.CampaignSendLogs
        //        .Where(log => log.CampaignId == campaignId && log.ContactId == contactId)
        //        .Select(log => new CampaignSendLogDto
        //        {
        //            Id = log.Id,
        //            CampaignId = log.CampaignId,
        //            ContactId = log.ContactId,
        //            MessageBody = log.MessageBody,
        //            TemplateId = log.TemplateId,
        //            SendStatus = log.SendStatus,
        //            ErrorMessage = log.ErrorMessage,
        //            CreatedAt = log.CreatedAt,
        //            SentAt = log.SentAt,
        //            DeliveredAt = log.DeliveredAt,
        //            ReadAt = log.ReadAt,
        //            IpAddress = log.IpAddress,
        //            DeviceInfo = log.DeviceInfo,
        //            MacAddress = log.MacAddress,
        //            SourceChannel = log.SourceChannel,
        //            IsClicked = log.IsClicked,
        //            ClickedAt = log.ClickedAt,
        //            ClickType = log.ClickType
        //        })
        //        .ToListAsync();
        //}

        // 🆕 Create a new send log (with enrichment)

        public async Task<PagedResult<CampaignSendLogDto>> GetLogsByCampaignIdAsync(
       Guid campaignId, string? status, string? search, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            // Base (scoped to the campaign)
            var q =
                from log in _context.CampaignSendLogs.AsNoTracking()
                where log.CampaignId == campaignId
                join ml in _context.MessageLogs.AsNoTracking()
                     on log.MessageLogId equals ml.Id into g
                from ml in g.DefaultIfEmpty() // LEFT JOIN
                select new { log, ml };

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(x => x.log.SendStatus == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                var kwLike = $"%{kw}%";

                q = q.Where(x =>
                    // match CRM contact name/phone if present
                    (x.log.Contact != null &&
                        (EF.Functions.ILike(x.log.Contact.Name!, kwLike) ||
                         x.log.Contact.PhoneNumber!.Contains(kw)))
                    ||
                    // match raw recipient number from message log (CSV-only etc.)
                    (x.ml.RecipientNumber != null && x.ml.RecipientNumber.Contains(kw))
                );
            }

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(x => x.log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new CampaignSendLogDto
                {
                    Id = x.log.Id,
                    // CampaignId = x.log.CampaignId ?? campaignId,
                    CampaignId = x.log.CampaignId,// ✅ fix
                    ContactId = x.log.ContactId,
                    ContactName = x.log.Contact != null ? x.log.Contact.Name : "N/A",
                    ContactPhone = x.log.Contact != null ? x.log.Contact.PhoneNumber : "-",
                    RecipientNumber = x.ml.RecipientNumber,
                    RecipientId = x.log.RecipientId,
                    MessageBody = x.log.MessageBody,
                    TemplateId = x.log.TemplateId,
                    SendStatus = x.log.SendStatus,
                    ErrorMessage = x.log.ErrorMessage,
                    CreatedAt = x.log.CreatedAt,
                    SentAt = x.log.SentAt,
                    DeliveredAt = x.log.DeliveredAt,
                    ReadAt = x.log.ReadAt,
                    SourceChannel = x.log.SourceChannel,
                    IsClicked = x.log.IsClicked,
                    ClickedAt = x.log.ClickedAt,
                    ClickType = x.log.ClickType
                })
                .ToListAsync();

            return new PagedResult<CampaignSendLogDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<CampaignSendLogDto>> GetLogsForContactAsync(Guid campaignId, Guid contactId)
        {
            return await _context.CampaignSendLogs
                .AsNoTracking()
               .Where(log =>
    log.CampaignId == campaignId &&
    log.ContactId == contactId)
                .Select(log => new CampaignSendLogDto
                {
                    Id = log.Id,
                    CampaignId = log.CampaignId,
                    ContactId = log.ContactId,  // unwrap after HasValue guard
                    MessageBody = log.MessageBody,
                    TemplateId = log.TemplateId,
                    SendStatus = log.SendStatus,
                    ErrorMessage = log.ErrorMessage,
                    CreatedAt = log.CreatedAt,
                    SentAt = log.SentAt,
                    DeliveredAt = log.DeliveredAt,
                    ReadAt = log.ReadAt,
                    IpAddress = log.IpAddress,
                    DeviceInfo = log.DeviceInfo,
                    MacAddress = log.MacAddress,
                    SourceChannel = log.SourceChannel,
                    IsClicked = log.IsClicked,
                    ClickedAt = log.ClickedAt,
                    ClickType = log.ClickType
                })
                .ToListAsync();
        }


        public async Task<bool> AddSendLogAsync(CampaignSendLogDto dto, string ipAddress, string userAgent)
        {
            var log = new CampaignSendLog
            {
                Id = Guid.NewGuid(),
                CampaignId = dto.CampaignId,
                ContactId = dto.ContactId,
                MessageBody = dto.MessageBody,
                TemplateId = dto.TemplateId,
                SendStatus = dto.SendStatus,
                ErrorMessage = dto.ErrorMessage,
                CreatedAt = DateTime.UtcNow,
                SentAt = dto.SentAt,
                DeliveredAt = dto.DeliveredAt,
                ReadAt = dto.ReadAt,
                SourceChannel = dto.SourceChannel,
                IsClicked = dto.IsClicked,
                ClickedAt = dto.ClickedAt,
                ClickType = dto.ClickType,
                RecipientId = dto.RecipientId
            };

            // ✅ Use enrichment service
            await _enricher.EnrichAsync(log, userAgent, ipAddress);

            _context.CampaignSendLogs.Add(log);
            await _context.SaveChangesAsync();
            return true;
        }

        // 📨 Update delivery or read status
        public async Task<bool> UpdateDeliveryStatusAsync(Guid logId, string status, DateTime? deliveredAt, DateTime? readAt)
        {
            var log = await _context.CampaignSendLogs.FirstOrDefaultAsync(l => l.Id == logId);
            if (log == null) return false;

            log.SendStatus = status;
            log.DeliveredAt = deliveredAt ?? log.DeliveredAt;
            log.ReadAt = readAt ?? log.ReadAt;

            await _context.SaveChangesAsync();
            return true;
        }

        // 📈 Track click (CTA)
        public async Task<bool> TrackClickAsync(Guid logId, string clickType)
        {
            var log = await _context.CampaignSendLogs.FirstOrDefaultAsync(l => l.Id == logId);
            if (log == null) return false;

            log.IsClicked = true;
            log.ClickedAt = DateTime.UtcNow;
            log.ClickType = clickType;

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<CampaignLogSummaryDto> GetCampaignSummaryAsync(Guid campaignId)
        {
            // This single, efficient query calculates all stats directly in the database.
            var summary = await _context.CampaignSendLogs
                .Where(l => l.CampaignId == campaignId)
                .GroupBy(l => 1) // Group by a constant to aggregate all results
                .Select(g => new
                {
                    TotalRecipients = g.Count(),

                    // CORRECTED LOGIC: A message is "Sent" if its status is NOT "Failed".
                    // This correctly includes messages that are "Sent", "Delivered", or "Read".
                    SentCount = g.Count(l => l.SendStatus != "Failed"),

                    FailedCount = g.Count(l => l.SendStatus == "Failed"),
                    ClickedCount = g.Count(l => l.IsClicked),
                    DeliveredCount = g.Count(l => l.DeliveredAt != null),
                    ReadCount = g.Count(l => l.ReadAt != null),
                    LastSentAt = g.Max(l => l.SentAt)
                })
                .FirstOrDefaultAsync();

            if (summary == null)
            {
                // Return an empty DTO if no logs are found for the campaign
                return new CampaignLogSummaryDto();
            }

            return new CampaignLogSummaryDto
            {
                TotalSent = summary.TotalRecipients,
                Sent = summary.SentCount,
                FailedCount = summary.FailedCount,
                ClickedCount = summary.ClickedCount,
                Delivered = summary.DeliveredCount,
                Read = summary.ReadCount,
                LastSentAt = summary.LastSentAt
            };
        }

    }
}
