using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.DTOs;
namespace xbytechat.api.Features.Catalog.Services
{
    public interface ICatalogDashboardService
    {
        Task<CatalogDashboardSummaryDto> GetDashboardSummaryAsync(Guid businessId);
        Task<List<TopProductDto>> GetTopClickedProductsAsync(Guid businessId, int topN = 5);
        Task<List<CtaJourneyStatsDto>> GetCtaJourneyStatsAsync(Guid businessId);
        Task<List<ProductCtaBreakdownDto>> GetProductCtaBreakdownAsync(Guid businessId);

    }
}