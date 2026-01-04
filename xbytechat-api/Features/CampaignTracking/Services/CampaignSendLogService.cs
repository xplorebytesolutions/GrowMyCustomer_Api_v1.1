using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using xbytechat.api;
using xbytechat.api.Features.CampaignTracking.DTOs;
using xbytechat.api.Features.CampaignTracking.Models;
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

        public async Task<PagedResult<CampaignSendLogDto>> GetLogsByCampaignIdAsync(
            Guid campaignId, string? status, string? search, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var q =
                from log in _context.CampaignSendLogs.AsNoTracking()
                where log.CampaignId == campaignId
                join ml in _context.MessageLogs.AsNoTracking()
                    on log.MessageLogId equals ml.Id into g
                from ml in g.DefaultIfEmpty()
                select new { log, ml };

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(x => x.log.SendStatus == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                var kwLike = $"%{kw}%";

                q = q.Where(x =>
                    (x.log.Contact != null &&
                        (EF.Functions.ILike(x.log.Contact.Name!, kwLike) ||
                         x.log.Contact.PhoneNumber!.Contains(kw)))
                    ||
                    (x.ml != null && x.ml.RecipientNumber != null && x.ml.RecipientNumber.Contains(kw))
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
                    CampaignId = x.log.CampaignId,
                    ContactId = x.log.ContactId,
                    ContactName = x.log.Contact != null ? x.log.Contact.Name : "N/A",
                    ContactPhone = x.log.Contact != null ? x.log.Contact.PhoneNumber : "-",
                    RecipientNumber = x.ml != null ? x.ml.RecipientNumber : null,

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
                    ClickType = x.log.ClickType,

                    // ✅ Retry mapping (DTO uses RetryStatus, model uses LastRetryStatus)
                    RetryStatus = x.log.LastRetryStatus,
                    RetryCount = x.log.RetryCount,
                    LastRetryAt = x.log.LastRetryAt,

                    IpAddress = x.log.IpAddress,
                    DeviceInfo = x.log.DeviceInfo,
                    MacAddress = x.log.MacAddress,

                    DeviceType = x.log.DeviceType,
                    Browser = x.log.Browser,
                    Country = x.log.Country,
                    City = x.log.City
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
                .Where(log => log.CampaignId == campaignId && log.ContactId == contactId)
                .OrderByDescending(log => log.CreatedAt)
                .Select(log => new CampaignSendLogDto
                {
                    Id = log.Id,
                    CampaignId = log.CampaignId,
                    ContactId = log.ContactId,

                    RecipientId = log.RecipientId,
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
                    ClickType = log.ClickType,

                    // ✅ Retry mapping
                    RetryStatus = log.LastRetryStatus,
                    RetryCount = log.RetryCount,
                    LastRetryAt = log.LastRetryAt
                })
                .ToListAsync();
        }

        // ✅ IMPORTANT: DTO does not carry BusinessId.
        // Derive BusinessId from Campaign for tenant-safe reporting.
        public async Task<bool> AddSendLogAsync(CampaignSendLogDto dto, string ipAddress, string userAgent)
        {
            var businessId = await _context.Campaigns
                .Where(c => c.Id == dto.CampaignId)
                .Select(c => c.BusinessId)
                .FirstOrDefaultAsync();

            if (businessId == Guid.Empty)
                return false;

            var log = new CampaignSendLog
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,

                CampaignId = dto.CampaignId,
                ContactId = dto.ContactId,

                RecipientId = dto.RecipientId,
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

                // ✅ Retry mapping
                RetryCount = dto.RetryCount,
                LastRetryAt = dto.LastRetryAt,
                LastRetryStatus = dto.RetryStatus
            };

            await _enricher.EnrichAsync(log, userAgent, ipAddress);

            _context.CampaignSendLogs.Add(log);
            await _context.SaveChangesAsync();
            return true;
        }

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

        public async Task<CampaignLogSummaryDto> GetCampaignSummaryAsync(
            Guid campaignId,
            DateTime? fromUtc,
            DateTime? toUtc,
            int repliedWindowDays,
            Guid? runId)
        {
            if (repliedWindowDays < 0) repliedWindowDays = 0;
            if (repliedWindowDays > 90) repliedWindowDays = 90;

            var logs = _context.CampaignSendLogs
                .AsNoTracking()
                .Where(l => l.CampaignId == campaignId);

            if (runId.HasValue)
                logs = logs.Where(l => l.RunId == runId.Value);

            if (fromUtc.HasValue)
                logs = logs.Where(l =>
                    (l.SentAt != null && l.SentAt >= fromUtc.Value) ||
                    (l.SentAt == null && l.CreatedAt >= fromUtc.Value));

            if (toUtc.HasValue)
                logs = logs.Where(l =>
                    (l.SentAt != null && l.SentAt <= toUtc.Value) ||
                    (l.SentAt == null && l.CreatedAt <= toUtc.Value));

            var baseSummary = await logs
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalRecipients = g.Count(),
                    SentCount = g.Count(l => l.SendStatus != "Failed"),
                    FailedCount = g.Count(l => l.SendStatus == "Failed"),
                    ClickedCount = g.Count(l => l.IsClicked),
                    DeliveredCount = g.Count(l => l.DeliveredAt != null),
                    ReadCount = g.Count(l => l.ReadAt != null),
                    LastSentAt = g.Max(l => l.SentAt)
                })
                .FirstOrDefaultAsync();

            if (baseSummary == null)
            {
                return new CampaignLogSummaryDto
                {
                    ReplyWindowDays = repliedWindowDays,
                    RepliedUniqueContacts = 0,
                    TotalSent = 0,
                    Sent = 0,
                    Delivered = 0,
                    Read = 0,
                    FailedCount = 0,
                    ClickedCount = 0,
                    LastSentAt = null
                };
            }

            // ✅ Business-safe replied metric
            var repliedUnique = await (
                from l in logs
                where l.ContactId != null
                let anchor = (l.SentAt ?? l.CreatedAt)
                join ml in _context.MessageLogs.AsNoTracking()
                    on l.ContactId equals ml.ContactId
                where ml.IsIncoming == true
                      && ml.BusinessId == l.BusinessId
                      && ml.CreatedAt >= anchor
                      && ml.CreatedAt <= anchor.AddDays(repliedWindowDays)
                select l.ContactId.Value
            ).Distinct().CountAsync();

            return new CampaignLogSummaryDto
            {
                TotalSent = baseSummary.TotalRecipients,
                Sent = baseSummary.SentCount,
                FailedCount = baseSummary.FailedCount,
                ClickedCount = baseSummary.ClickedCount,
                Delivered = baseSummary.DeliveredCount,
                Read = baseSummary.ReadCount,
                LastSentAt = baseSummary.LastSentAt,

                RepliedUniqueContacts = repliedUnique,
                ReplyWindowDays = repliedWindowDays
            };
        }

        private async Task<long> ExecuteScalarLongAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
        {
            var connString = _context.Database.GetDbConnection().ConnectionString;

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        public async Task<PagedResult<CampaignContactListItemDto>> GetContactsByStatBucketAsync(
            Guid campaignId,
            string bucket,
            DateTime? fromUtc,
            DateTime? toUtc,
            int repliedWindowDays,
            Guid? runId,
            string? search,
            int page,
            int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            bucket = (bucket ?? "").Trim().ToLowerInvariant();
            if (repliedWindowDays < 0) repliedWindowDays = 0;
            if (repliedWindowDays > 90) repliedWindowDays = 90;

            var kw = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var kwLike = kw == null ? null : $"%{kw}%";
            var offset = (page - 1) * pageSize;

            string bucketWhere = bucket switch
            {
                "sent" => @"AND l.""SendStatus"" <> 'Failed'",
                "failed" => @"AND l.""SendStatus"" = 'Failed'",
                "delivered" => @"AND l.""DeliveredAt"" IS NOT NULL",
                "read" => @"AND l.""ReadAt"" IS NOT NULL",
                "clicked" => @"AND l.""IsClicked"" = TRUE",
                "replied" => @"",
                _ => @""
            };

            const string commonBaseFilters = @"
WHERE l.""CampaignId"" = @campaignId
  AND (@runId IS NULL OR l.""RunId"" = @runId)
  AND (
        @fromUtc IS NULL OR
        ((l.""SentAt"" IS NOT NULL AND l.""SentAt"" >= @fromUtc) OR (l.""SentAt"" IS NULL AND l.""CreatedAt"" >= @fromUtc))
      )
  AND (
        @toUtc IS NULL OR
        ((l.""SentAt"" IS NOT NULL AND l.""SentAt"" <= @toUtc) OR (l.""SentAt"" IS NULL AND l.""CreatedAt"" <= @toUtc))
      )
";

            const string searchFilter = @"
  AND (
        @kw IS NULL OR
        (c.""Name"" IS NOT NULL AND c.""Name"" ILIKE @kwLike) OR
        (c.""PhoneNumber"" IS NOT NULL AND c.""PhoneNumber"" LIKE '%' || @kw || '%') OR
        (ml.""RecipientNumber"" IS NOT NULL AND ml.""RecipientNumber"" LIKE '%' || @kw || '%')
      )
";

            if (bucket == "replied")
            {
                var countSql = $@"
WITH base AS (
    SELECT
        l.""Id""                           AS ""SendLogId"",
        l.""MessageLogId""                 AS ""MessageLogId"",
        l.""RecipientId""                  AS ""RecipientId"",
        l.""BusinessId""                   AS ""BusinessId"",
        l.""ContactId""                    AS ""ContactId"",
        COALESCE(c.""Name"", 'N/A')        AS ""ContactName"",
        COALESCE(c.""PhoneNumber"", '-')   AS ""ContactPhone"",
        ml.""RecipientNumber""             AS ""RecipientNumber"",
        l.""SendStatus""                   AS ""SendStatus"",
        l.""ErrorMessage""                 AS ""ErrorMessage"",
        l.""IsClicked""                    AS ""IsClicked"",
        l.""ClickType""                    AS ""ClickType"",
        l.""ClickedAt""                    AS ""ClickedAt"",
        l.""SentAt""                       AS ""SentAt"",
        l.""DeliveredAt""                  AS ""DeliveredAt"",
        l.""ReadAt""                       AS ""ReadAt"",
        COALESCE(l.""SentAt"", l.""CreatedAt"") AS ""Anchor"",
        COALESCE(l.""ReadAt"", l.""DeliveredAt"", l.""SentAt"", l.""ClickedAt"", l.""CreatedAt"") AS ""LastUpdatedAt""
    FROM ""CampaignSendLogs"" l
    LEFT JOIN ""MessageLogs"" ml ON ml.""Id"" = l.""MessageLogId""
    LEFT JOIN ""Contacts"" c     ON c.""Id""  = l.""ContactId""
    {commonBaseFilters}
    AND l.""ContactId"" IS NOT NULL
    {searchFilter}
),
dedup AS (
    SELECT DISTINCT ON (""ContactId"")
        *
    FROM base
    ORDER BY ""ContactId"", ""LastUpdatedAt"" DESC
),
replied AS (
    SELECT
        d.*,
        inbound.""LastInboundAt"" AS ""LastInboundAt""
    FROM dedup d
    JOIN LATERAL (
        SELECT MAX(m.""CreatedAt"") AS ""LastInboundAt""
        FROM ""MessageLogs"" m
        WHERE m.""BusinessId"" = d.""BusinessId""
          AND m.""ContactId""  = d.""ContactId""
          AND m.""IsIncoming"" = TRUE
          AND m.""CreatedAt"" >= d.""Anchor""
          AND m.""CreatedAt"" <= d.""Anchor"" + make_interval(days => @repliedWindowDays)
    ) inbound ON inbound.""LastInboundAt"" IS NOT NULL
)
SELECT COUNT(*)
FROM replied;
";

                var itemsSql = $@"
WITH base AS (
    SELECT
        l.""Id""                           AS ""SendLogId"",
        l.""MessageLogId""                 AS ""MessageLogId"",
        l.""RecipientId""                  AS ""RecipientId"",
        l.""BusinessId""                   AS ""BusinessId"",
        l.""ContactId""                    AS ""ContactId"",
        COALESCE(c.""Name"", 'N/A')        AS ""ContactName"",
        COALESCE(c.""PhoneNumber"", '-')   AS ""ContactPhone"",
        ml.""RecipientNumber""             AS ""RecipientNumber"",
        l.""SendStatus""                   AS ""SendStatus"",
        l.""ErrorMessage""                 AS ""ErrorMessage"",
        l.""IsClicked""                    AS ""IsClicked"",
        l.""ClickType""                    AS ""ClickType"",
        l.""ClickedAt""                    AS ""ClickedAt"",
        l.""SentAt""                       AS ""SentAt"",
        l.""DeliveredAt""                  AS ""DeliveredAt"",
        l.""ReadAt""                       AS ""ReadAt"",
        COALESCE(l.""SentAt"", l.""CreatedAt"") AS ""Anchor"",
        COALESCE(l.""ReadAt"", l.""DeliveredAt"", l.""SentAt"", l.""ClickedAt"", l.""CreatedAt"") AS ""LastUpdatedAt""
    FROM ""CampaignSendLogs"" l
    LEFT JOIN ""MessageLogs"" ml ON ml.""Id"" = l.""MessageLogId""
    LEFT JOIN ""Contacts"" c     ON c.""Id""  = l.""ContactId""
    {commonBaseFilters}
    AND l.""ContactId"" IS NOT NULL
    {searchFilter}
),
dedup AS (
    SELECT DISTINCT ON (""ContactId"")
        *
    FROM base
    ORDER BY ""ContactId"", ""LastUpdatedAt"" DESC
),
replied AS (
    SELECT
        d.*,
        inbound.""LastInboundAt"" AS ""LastInboundAt""
    FROM dedup d
    JOIN LATERAL (
        SELECT MAX(m.""CreatedAt"") AS ""LastInboundAt""
        FROM ""MessageLogs"" m
        WHERE m.""BusinessId"" = d.""BusinessId""
          AND m.""ContactId""  = d.""ContactId""
          AND m.""IsIncoming"" = TRUE
          AND m.""CreatedAt"" >= d.""Anchor""
          AND m.""CreatedAt"" <= d.""Anchor"" + make_interval(days => @repliedWindowDays)
    ) inbound ON inbound.""LastInboundAt"" IS NOT NULL
)
SELECT
    ""SendLogId""        AS ""SendLogId"",
    ""MessageLogId""     AS ""MessageLogId"",
    ""RecipientId""      AS ""RecipientId"",
    ""ContactId""        AS ""ContactId"",
    ""ContactName""      AS ""ContactName"",
    ""ContactPhone""     AS ""ContactPhone"",
    ""RecipientNumber""  AS ""RecipientNumber"",
    ""SendStatus""       AS ""SendStatus"",
    ""ErrorMessage""     AS ""ErrorMessage"",
    ""IsClicked""        AS ""IsClicked"",
    ""ClickType""        AS ""ClickType"",
    ""ClickedAt""        AS ""ClickedAt"",
    ""SentAt""           AS ""SentAt"",
    ""DeliveredAt""      AS ""DeliveredAt"",
    ""ReadAt""           AS ""ReadAt"",
    ""LastInboundAt""    AS ""LastInboundAt"",
    ""LastUpdatedAt""    AS ""LastUpdatedAt""
FROM replied
ORDER BY ""LastInboundAt"" DESC
OFFSET @offset
LIMIT @pageSize;
";

                // ✅ Use SqlQueryRaw + object[] => no “takes 9 arguments” compile error.
                var total = await _context.Database
                    .SqlQueryRaw<long>(
                        countSql,
                        new object[]
                        {
                            new NpgsqlParameter("campaignId", campaignId),
                            new NpgsqlParameter("runId", (object?)runId ?? DBNull.Value),
                            new NpgsqlParameter("fromUtc", (object?)fromUtc ?? DBNull.Value),
                            new NpgsqlParameter("toUtc", (object?)toUtc ?? DBNull.Value),
                            new NpgsqlParameter("kw", (object?)kw ?? DBNull.Value),
                            new NpgsqlParameter("kwLike", (object?)kwLike ?? DBNull.Value),
                            new NpgsqlParameter("repliedWindowDays", repliedWindowDays)
                        })
                    .SingleAsync();

                var items = await _context.Database
                    .SqlQueryRaw<CampaignContactListItemDto>(
                        itemsSql,
                        new object[]
                        {
                            new NpgsqlParameter("campaignId", campaignId),
                            new NpgsqlParameter("runId", (object?)runId ?? DBNull.Value),
                            new NpgsqlParameter("fromUtc", (object?)fromUtc ?? DBNull.Value),
                            new NpgsqlParameter("toUtc", (object?)toUtc ?? DBNull.Value),
                            new NpgsqlParameter("kw", (object?)kw ?? DBNull.Value),
                            new NpgsqlParameter("kwLike", (object?)kwLike ?? DBNull.Value),
                            new NpgsqlParameter("repliedWindowDays", repliedWindowDays),
                            new NpgsqlParameter("offset", offset),
                            new NpgsqlParameter("pageSize", pageSize)
                        })
                    .ToListAsync();

                return new PagedResult<CampaignContactListItemDto>
                {
                    Items = items,
                    TotalCount = (int)total,
                    Page = page,
                    PageSize = pageSize
                };
            }
            else
            {
                var countSql = $@"
WITH base AS (
    SELECT
        l.""Id""                           AS ""SendLogId"",
        l.""MessageLogId""                 AS ""MessageLogId"",
        l.""RecipientId""                  AS ""RecipientId"",
        l.""ContactId""                    AS ""ContactId"",
        COALESCE(c.""Name"", 'N/A')        AS ""ContactName"",
        COALESCE(c.""PhoneNumber"", '-')   AS ""ContactPhone"",
        ml.""RecipientNumber""             AS ""RecipientNumber"",
        l.""SendStatus""                   AS ""SendStatus"",
        l.""ErrorMessage""                 AS ""ErrorMessage"",
        l.""IsClicked""                    AS ""IsClicked"",
        l.""ClickType""                    AS ""ClickType"",
        l.""ClickedAt""                    AS ""ClickedAt"",
        l.""SentAt""                       AS ""SentAt"",
        l.""DeliveredAt""                  AS ""DeliveredAt"",
        l.""ReadAt""                       AS ""ReadAt"",
        COALESCE(l.""ReadAt"", l.""DeliveredAt"", l.""SentAt"", l.""ClickedAt"", l.""CreatedAt"") AS ""LastUpdatedAt"",
        COALESCE(l.""ContactId""::text, ml.""RecipientNumber"") AS ""GroupKey""
    FROM ""CampaignSendLogs"" l
    LEFT JOIN ""MessageLogs"" ml ON ml.""Id"" = l.""MessageLogId""
    LEFT JOIN ""Contacts"" c     ON c.""Id""  = l.""ContactId""
    {commonBaseFilters}
    {bucketWhere}
    {searchFilter}
),
dedup AS (
    SELECT DISTINCT ON (""GroupKey"")
        *
    FROM base
    ORDER BY ""GroupKey"", ""LastUpdatedAt"" DESC
)
SELECT COUNT(*) FROM dedup;
";

                var itemsSql = $@"
WITH base AS (
    SELECT
        l.""Id""                           AS ""SendLogId"",
        l.""MessageLogId""                 AS ""MessageLogId"",
        l.""RecipientId""                  AS ""RecipientId"",
        l.""ContactId""                    AS ""ContactId"",
        COALESCE(c.""Name"", 'N/A')        AS ""ContactName"",
        COALESCE(c.""PhoneNumber"", '-')   AS ""ContactPhone"",
        ml.""RecipientNumber""             AS ""RecipientNumber"",
        l.""SendStatus""                   AS ""SendStatus"",
        l.""ErrorMessage""                 AS ""ErrorMessage"",
        l.""IsClicked""                    AS ""IsClicked"",
        l.""ClickType""                    AS ""ClickType"",
        l.""ClickedAt""                    AS ""ClickedAt"",
        l.""SentAt""                       AS ""SentAt"",
        l.""DeliveredAt""                  AS ""DeliveredAt"",
        l.""ReadAt""                       AS ""ReadAt"",
        COALESCE(l.""ReadAt"", l.""DeliveredAt"", l.""SentAt"", l.""ClickedAt"", l.""CreatedAt"") AS ""LastUpdatedAt"",
        COALESCE(l.""ContactId""::text, ml.""RecipientNumber"") AS ""GroupKey""
    FROM ""CampaignSendLogs"" l
    LEFT JOIN ""MessageLogs"" ml ON ml.""Id"" = l.""MessageLogId""
    LEFT JOIN ""Contacts"" c     ON c.""Id""  = l.""ContactId""
    {commonBaseFilters}
    {bucketWhere}
    {searchFilter}
),
dedup AS (
    SELECT DISTINCT ON (""GroupKey"")
        *
    FROM base
    ORDER BY ""GroupKey"", ""LastUpdatedAt"" DESC
)
SELECT
    ""SendLogId""        AS ""SendLogId"",
    ""MessageLogId""     AS ""MessageLogId"",
    ""RecipientId""      AS ""RecipientId"",
    ""ContactId""        AS ""ContactId"",
    ""ContactName""      AS ""ContactName"",
    ""ContactPhone""     AS ""ContactPhone"",
    ""RecipientNumber""  AS ""RecipientNumber"",
    ""SendStatus""       AS ""SendStatus"",
    ""ErrorMessage""     AS ""ErrorMessage"",
    ""IsClicked""        AS ""IsClicked"",
    ""ClickType""        AS ""ClickType"",
    ""ClickedAt""        AS ""ClickedAt"",
    ""SentAt""           AS ""SentAt"",
    ""DeliveredAt""      AS ""DeliveredAt"",
    ""ReadAt""           AS ""ReadAt"",
    NULL::timestamptz    AS ""LastInboundAt"",
    ""LastUpdatedAt""    AS ""LastUpdatedAt""
FROM dedup
ORDER BY ""LastUpdatedAt"" DESC
OFFSET @offset
LIMIT @pageSize;
";

                var total = await _context.Database
                    .SqlQueryRaw<long>(
                        countSql,
                        new object[]
                        {
                            new NpgsqlParameter("campaignId", campaignId),
                            new NpgsqlParameter("runId", (object?)runId ?? DBNull.Value),
                            new NpgsqlParameter("fromUtc", (object?)fromUtc ?? DBNull.Value),
                            new NpgsqlParameter("toUtc", (object?)toUtc ?? DBNull.Value),
                            new NpgsqlParameter("kw", (object?)kw ?? DBNull.Value),
                            new NpgsqlParameter("kwLike", (object?)kwLike ?? DBNull.Value)
                        })
                    .SingleAsync();

                var items = await _context.Database
                    .SqlQueryRaw<CampaignContactListItemDto>(
                        itemsSql,
                        new object[]
                        {
                            new NpgsqlParameter("campaignId", campaignId),
                            new NpgsqlParameter("runId", (object?)runId ?? DBNull.Value),
                            new NpgsqlParameter("fromUtc", (object?)fromUtc ?? DBNull.Value),
                            new NpgsqlParameter("toUtc", (object?)toUtc ?? DBNull.Value),
                            new NpgsqlParameter("kw", (object?)kw ?? DBNull.Value),
                            new NpgsqlParameter("kwLike", (object?)kwLike ?? DBNull.Value),
                            new NpgsqlParameter("offset", offset),
                            new NpgsqlParameter("pageSize", pageSize)
                        })
                    .ToListAsync();

                return new PagedResult<CampaignContactListItemDto>
                {
                    Items = items,
                    TotalCount = (int)total,
                    Page = page,
                    PageSize = pageSize
                };
            }
        }
    }
}
