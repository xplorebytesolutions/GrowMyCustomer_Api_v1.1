using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Catalog.DTOs;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace xbytechat.api.Features.Catalog.Services
{
    public class CatalogDashboardService : ICatalogDashboardService
    {
        private readonly AppDbContext _context;

        public CatalogDashboardService(AppDbContext context)
        {
            _context = context;
        }

       
        public async Task<CatalogDashboardSummaryDto> GetDashboardSummaryAsync(Guid businessId)
        {
            var today = DateTime.UtcNow.Date;

            var totalMessagesSent = await _context.MessageLogs
                .CountAsync(m => m.BusinessId == businessId);

            var uniqueCustomersMessaged = await _context.MessageLogs
                .Where(m => m.BusinessId == businessId)
                .Select(m => m.RecipientNumber)
                .Distinct()
                .CountAsync();

            var productClicks = await _context.CatalogClickLogs
                .CountAsync(c => c.BusinessId == businessId && c.ProductId != null);

            var activeProducts = await _context.Products
                .CountAsync(p => p.BusinessId == businessId);

            var productsSharedViaWhatsapp = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId && c.ProductId != null)
                .Select(c => c.ProductId)
                .Distinct()
                .CountAsync();

            var repeatClickers = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId)
                .GroupBy(c => c.UserPhone)
                .CountAsync(g => g.Count() > 1);

            var newClickersToday = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId && c.ClickedAt.Value.Date == today)
                .Select(c => c.UserPhone)
                .Distinct()
                .CountAsync();
            //        var newClickersToday = _context.CatalogClickLogs
            //.Where(c => c.BusinessId == businessId)
            //.ToList() // now it's LINQ to Objects
            //        .Where(c => c.ClickedAt?.Date == today)
            //        .Select(c => c.UserPhone)
            //        .Distinct()
            //.Count();
            //        But if your data is large, the first(server-side filtering) is the better choice.

            var lastCatalogClickAt = await _context.CatalogClickLogs
    .Where(c => c.BusinessId == businessId)
    .MaxAsync(c => (DateTime?)c.ClickedAt);

            var lastMessageSentAt = await _context.MessageLogs
                .Where(m => m.BusinessId == businessId)
                .MaxAsync(m => (DateTime?)m.SentAt);

            return new CatalogDashboardSummaryDto
            {
                TotalMessagesSent = totalMessagesSent,
                UniqueCustomersMessaged = uniqueCustomersMessaged,
                ProductClicks = productClicks,
                ActiveProducts = activeProducts,
                ProductsSharedViaWhatsApp = productsSharedViaWhatsapp,
                RepeatClickers = repeatClickers,
                NewClickersToday = newClickersToday,
                LastCatalogClickAt = lastCatalogClickAt,
                LastMessageSentAt = lastMessageSentAt
            };
        }

        public async Task<List<TopProductDto>> GetTopClickedProductsAsync(Guid businessId, int topN = 5)
        {
            var topProducts = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId && c.ProductId != null)
                .GroupBy(c => c.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ClickCount = g.Count()
                })
                .OrderByDescending(x => x.ClickCount)
                .Take(topN)
                .ToListAsync();

            // Now fetch product names to join with clicks
            var productIds = topProducts.Select(x => x.ProductId).ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            return topProducts.Select(x => new TopProductDto
            {
                ProductId = x.ProductId.Value,
                ProductName = products.ContainsKey(x.ProductId.Value) ? products[x.ProductId.Value] : "Unknown",
                ClickCount = x.ClickCount
            }).ToList();
        }
        public async Task<List<CtaJourneyStatsDto>> GetCtaJourneyStatsAsync(Guid businessId)
        {
            var stats = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId && !string.IsNullOrEmpty(c.CTAJourney))
                .GroupBy(c => c.CTAJourney)
                .Select(g => new CtaJourneyStatsDto
                {
                    CTAJourney = g.Key,
                    ClickCount = g.Count()
                })
                .OrderByDescending(x => x.ClickCount)
                .ToListAsync();

            return stats;
        }
        public async Task<List<ProductCtaBreakdownDto>> GetProductCtaBreakdownAsync(Guid businessId)
        {
            var groupedClicks = await _context.CatalogClickLogs
                .Where(c => c.BusinessId == businessId && c.ProductId != null && !string.IsNullOrEmpty(c.CTAJourney))
                .GroupBy(c => new { c.ProductId, c.CTAJourney })
                .Select(g => new
                {
                    ProductId = g.Key.ProductId.Value,
                    CTAJourney = g.Key.CTAJourney,
                    ClickCount = g.Count()
                })
                .ToListAsync();

            // Fetch product names for all involved productIds
            var productIds = groupedClicks.Select(g => g.ProductId).Distinct().ToList();

            var productNames = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            var result = groupedClicks.Select(g => new ProductCtaBreakdownDto
            {
                ProductId = g.ProductId,
                ProductName = productNames.ContainsKey(g.ProductId) ? productNames[g.ProductId] : "Unknown",
                CTAJourney = g.CTAJourney,
                ClickCount = g.ClickCount
            }).ToList();

            return result;
        }

    }
}