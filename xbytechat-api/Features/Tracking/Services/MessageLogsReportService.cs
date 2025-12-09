using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Tracking.Services
{
    

    // Strongly-typed intermediate row for EF translation (avoid 'dynamic')
    internal sealed class MessageLogRow
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }

        public string? RecipientNumber { get; set; }
        public string? SenderId { get; set; }           // Campaign.PhoneNumberId
        public string? SourceChannel { get; set; }      // Campaign.Provider OR MessageLog.Provider
        public string? Status { get; set; }

        public string? MessageContent { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public sealed class MessageLogsReportService : IMessageLogsReportService
    {
        private readonly AppDbContext _db;
        public MessageLogsReportService(AppDbContext db) => _db = db;

        public async Task<PaginatedResponse<MessageLogListItemDto>> SearchAsync(
            Guid businessId, MessageLogReportQueryDto q, CancellationToken ct)
        {
            // normalize paging
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            // Base + left join to Campaign to enrich
            var baseQuery =
                from m in _db.MessageLogs.AsNoTracking()
                where m.BusinessId == businessId
                join c0 in _db.Campaigns.AsNoTracking() on m.CampaignId equals c0.Id into cj
                from c in cj.DefaultIfEmpty()
                select new MessageLogRow
                {
                    Id = m.Id,
                    BusinessId = m.BusinessId,
                    CampaignId = m.CampaignId,
                    CampaignName = c != null ? c.Name : null,
                    RecipientNumber = m.RecipientNumber,
                    SenderId = c != null ? c.PhoneNumberId : null,
                    SourceChannel = (c != null && c.Provider != null) ? c.Provider : m.Provider,
                    Status = m.Status,
                    MessageContent = m.MessageContent,
                    ProviderMessageId = m.ProviderMessageId ?? m.MessageId,
                    ErrorMessage = m.ErrorMessage,
                    CreatedAt = m.CreatedAt,
                    SentAt = m.SentAt
                };

            // Time window (prefer SentAt over CreatedAt)
            if (q.FromUtc.HasValue)
                baseQuery = baseQuery.Where(x => (x.SentAt ?? x.CreatedAt) >= q.FromUtc.Value);
            if (q.ToUtc.HasValue)
                baseQuery = baseQuery.Where(x => (x.SentAt ?? x.CreatedAt) <= q.ToUtc.Value);

            // Optional scope
            if (q.CampaignId.HasValue)
                baseQuery = baseQuery.Where(x => x.CampaignId == q.CampaignId.Value);

            // Status filter
            if (q.Statuses is { Length: > 0 })
            {
                var statuses = q.Statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (statuses.Length > 0) baseQuery = baseQuery.Where(x => statuses.Contains(x.Status!));
            }

            // Channel filter (campaign.Provider preferred, else message.Provider)
            if (q.Channels is { Length: > 0 })
            {
                var chans = q.Channels.Select(s => s.Trim().ToUpperInvariant())
                                      .Where(s => s.Length > 0).ToArray();
                if (chans.Length > 0)
                    baseQuery = baseQuery.Where(x => x.SourceChannel != null &&
                                                     chans.Contains(x.SourceChannel.ToUpper()));
            }

            // SenderIds → Campaign.PhoneNumberId
            if (q.SenderIds is { Length: > 0 })
            {
                var senders = q.SenderIds.Select(s => s.Trim())
                                         .Where(s => s.Length > 0).ToArray();
                if (senders.Length > 0)
                    baseQuery = baseQuery.Where(x => x.SenderId != null && senders.Contains(x.SenderId));
            }

            // Free-text search
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.RecipientNumber ?? "").ToLower().Contains(s) ||
                    (x.MessageContent ?? "").ToLower().Contains(s) ||
                    (x.ErrorMessage ?? "").ToLower().Contains(s) ||
                    (x.ProviderMessageId ?? "").ToLower().Contains(s) ||
                    (x.CampaignName ?? "").ToLower().Contains(s));
            }

            // ----- Strongly-typed sorting -----
            var sortBy = (q.SortBy ?? "SentAt").Trim();
            var sortDir = (q.SortDir ?? "desc").Trim().ToLower() == "asc" ? "asc" : "desc";

            var sortMap = new Dictionary<string, Expression<Func<MessageLogRow, object>>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Recipient"] = x => x.RecipientNumber ?? "",
                ["SenderId"] = x => x.SenderId ?? "",
                ["Channel"] = x => x.SourceChannel ?? "",
                ["Status"] = x => x.Status ?? "",
                ["CampaignName"] = x => x.CampaignName ?? "",
                ["CreatedAt"] = x => x.CreatedAt,
                ["SentAt"] = x => x.SentAt ?? x.CreatedAt
            };

            if (!sortMap.ContainsKey(sortBy)) sortBy = "SentAt";
            var keySelector = sortMap[sortBy];

            IOrderedQueryable<MessageLogRow> ordered =
                sortDir == "asc" ? baseQuery.OrderBy(keySelector)
                                 : baseQuery.OrderByDescending(keySelector);

            var total = await ordered.CountAsync(ct);

            var items = await ordered
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(x => new MessageLogListItemDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    CampaignId = x.CampaignId,
                    CampaignName = x.CampaignName,
                    RecipientNumber = x.RecipientNumber,
                    SenderId = x.SenderId,
                    SourceChannel = x.SourceChannel,
                    Status = x.Status,
                    MessageType = null,             // not on MessageLog (can be enriched later)
                    MessageContent = x.MessageContent,
                    TemplateName = null,             // not on MessageLog
                    ProviderMessageId = x.ProviderMessageId,
                    ErrorMessage = x.ErrorMessage,
                    CreatedAt = x.CreatedAt,
                    SentAt = x.SentAt,
                    DeliveredAt = null,             // not on MessageLog
                    ReadAt = null              // not on MessageLog
                })
                .ToListAsync(ct);

            return new PaginatedResponse<MessageLogListItemDto>
            {
                Items = items,
                TotalCount = total,
                Page = q.Page,
                PageSize = q.PageSize
            };
        }
        public async Task<MessageLogFacetsDto> GetFacetsAsync(Guid businessId, DateTime? fromUtc, CancellationToken ct)
        {
            // scope last 90 days by default to keep it fast
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-90);

            // base query limited by business & time
            var baseQ = _db.MessageLogs
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId &&
                            (m.SentAt ?? m.CreatedAt) >= from);

            // SenderIds come from Campaign.PhoneNumberId (if available)

            var senderIds = await _db.WhatsAppPhoneNumbers
            .AsNoTracking()
            .Where(w => w.BusinessId == businessId && w.WhatsAppBusinessNumber != null)
            .Select(w => w.WhatsAppBusinessNumber!)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);

            //var senderIds = await (
            //    from m in baseQ
            //    join c0 in _db.Campaigns.AsNoTracking() on m.CampaignId equals c0.Id into cj
            //    from c in cj.DefaultIfEmpty()
            //    where c != null && c.PhoneNumberId != null
            //    select c.PhoneNumberId!
            //)
            //.Distinct()
            //.OrderBy(x => x)
            //.ToArrayAsync(ct);

            // Channels = provider (prefer campaign.provider, else message.provider)
            var channels = await (
                from m in baseQ
                join c0 in _db.Campaigns.AsNoTracking() on m.CampaignId equals c0.Id into cj
                from c in cj.DefaultIfEmpty()
                let prov = (c != null && c.Provider != null) ? c.Provider : m.Provider
                where prov != null
                select prov!
            )
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);

            // Statuses
            var statuses = await baseQ
                .Where(m => m.Status != null)
                .Select(m => m.Status!)
                .Distinct()
                .OrderBy(x => x)
                .ToArrayAsync(ct);

            // WABA Ids = WhatsAppBusinessNumber for this business
            var wabaIds = await _db.WhatsAppSettings
                .AsNoTracking()
                .Where(w => w.BusinessId == businessId && w.WabaId != null)
                .Select(w => w.WabaId!)
                .Distinct()
                .OrderBy(x => x)
                .ToArrayAsync(ct);

            return new MessageLogFacetsDto
            {
                WabaIds = wabaIds,
                SenderIds = senderIds,
                Channels = channels,
                Statuses = statuses
            };
        }
    }
}


