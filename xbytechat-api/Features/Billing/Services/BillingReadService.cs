using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

using xbytechat.api;                               // AppDbContext
using xbytechat_api.Features.Billing.DTOs;         // BillingSnapshotDto
using xbytechat_api.Features.Billing.Models;       // ProviderBillingEvent (for _db.ProviderBillingEvents)

namespace xbytechat_api.Features.Billing.Services
{
    public class BillingReadService : IBillingReadService
    {
        private readonly AppDbContext _db;
        public BillingReadService(AppDbContext db) => _db = db;

        public async Task<BillingSnapshotDto> GetBusinessBillingSnapshotAsync(Guid businessId, DateOnly from, DateOnly to)
        {
            // Build inclusive [from..to] range in UTC
            var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toDt = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            // 1) Volume: total messages in the period (unchanged behavior)
            var totalMessages = await _db.MessageLogs.AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.CreatedAt >= fromDt && x.CreatedAt <= toDt)
                .CountAsync();

            // 2) Billing source of truth: pricing_update events in the period
            //    (Only select small projection; we'll dedupe in-memory safely.)
            var evRaw = await _db.ProviderBillingEvents.AsNoTracking()
                .Where(e => e.BusinessId == businessId
                            && e.EventType == "pricing_update"
                            && e.OccurredAt >= fromDt && e.OccurredAt <= toDt)
                .Select(e => new {
                    e.Provider,
                    e.ProviderMessageId,
                    e.EventType,
                    e.ConversationId,
                    e.ConversationCategory,
                    e.IsChargeable,
                    e.PriceAmount,
                    e.PriceCurrency
                })
                .ToListAsync();

            // 2a) Defend against webhook replays (if DB unique index not yet deployed)
            //     Deduplicate on Provider+ProviderMessageId+EventType to drop repeats of the same message event.
            var evDedup = evRaw
                .GroupBy(e => new { e.Provider, e.ProviderMessageId, e.EventType })
                .Select(g => g.First())
                .ToList();

            // 2b) Group by conversation to compute window-level metrics
            var convGroups = evDedup
                .Where(e => !string.IsNullOrWhiteSpace(e.ConversationId))
                .GroupBy(e => e.ConversationId!)
                .ToList();

            // Chargeable windows: any event in the conversation marked billable == true
            var chargeableWindows = convGroups.Count(g => g.Any(x => x.IsChargeable == true));

            // Free windows: conversations explicitly marked billable == false and NOT marked true anywhere
            var freeWindows = convGroups.Count(g => g.Any(x => x.IsChargeable == false) && !g.Any(x => x.IsChargeable == true));

            // Count by category (per conversation, pick first non-empty category; default "unknown")
            var countByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in convGroups)
            {
                var category = g.Select(x => x.ConversationCategory)
                                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                                ?.ToLowerInvariant() ?? "unknown";
                countByCategory[category] = countByCategory.TryGetValue(category, out var c) ? c + 1 : 1;
            }

            // Spend by currency: for each conversation, take the latest non-null amount (if any), then sum by currency
            var spendByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in convGroups)
            {
                // Prefer an explicit currency on any event in the conversation
                var amountWithCurrency = g.LastOrDefault(x => x.PriceAmount.HasValue && !string.IsNullOrWhiteSpace(x.PriceCurrency));
                if (amountWithCurrency?.IsChargeable == true) // only count billable windows
                {
                    var cur = amountWithCurrency.PriceCurrency!.ToUpperInvariant();
                    var amt = amountWithCurrency.PriceAmount!.Value;
                    spendByCurrency[cur] = spendByCurrency.TryGetValue(cur, out var sum) ? sum + amt : amt;
                }
            }

            // Compose DTO
            var dto = new BillingSnapshotDto
            {
                TotalMessages = totalMessages,
                // These two are now "window"-level metrics (conversations) – most accurate for billing with Meta.
                ChargeableMessages = chargeableWindows,
                FreeMessages = freeWindows,
                CountByCategory = countByCategory,
                SpendByCurrency = spendByCurrency
            };

            return dto;
        }
    }
}


//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using xbytechat.api;
//using xbytechat_api.Features.Billing.DTOs;

//namespace xbytechat_api.Features.Billing.Services
//{
//    public class BillingReadService : IBillingReadService
//    {
//        private readonly AppDbContext _db;
//        public BillingReadService(AppDbContext db) => _db = db;

//        public async Task<BillingSnapshotDto> GetBusinessBillingSnapshotAsync(Guid businessId, DateOnly from, DateOnly to)
//        {
//            var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
//            var toDt = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

//            var q = _db.MessageLogs.AsNoTracking()
//                    .Where(x => x.BusinessId == businessId && x.CreatedAt >= fromDt && x.CreatedAt <= toDt);

//            var list = await q.Select(x => new {
//                x.IsChargeable,
//                x.ConversationCategory,
//                x.PriceAmount,
//                x.PriceCurrency
//            }).ToListAsync();

//            var dto = new BillingSnapshotDto
//            {
//                TotalMessages = list.Count,
//                ChargeableMessages = list.Count(x => x.IsChargeable == true),
//                FreeMessages = list.Count(x => x.IsChargeable == false)
//            };

//            dto.CountByCategory = list
//                .GroupBy(x => string.IsNullOrWhiteSpace(x.ConversationCategory) ? "unknown" : x.ConversationCategory!.ToLowerInvariant())
//                .ToDictionary(g => g.Key, g => g.Count());

//            dto.SpendByCurrency = list
//                .Where(x => x.PriceAmount.HasValue && !string.IsNullOrWhiteSpace(x.PriceCurrency))
//                .GroupBy(x => x.PriceCurrency!)
//                .ToDictionary(g => g.Key, g => g.Sum(v => v.PriceAmount!.Value));

//            return dto;
//        }
//    }
//}
