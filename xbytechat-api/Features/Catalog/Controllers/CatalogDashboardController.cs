using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Services;
using System;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Catalog.Controllers
{
    [ApiController]
    [Route("api/catalog-dashboard")]
    public class CatalogDashboardController : ControllerBase
    {
        private readonly ICatalogDashboardService _dashboardService;

        public CatalogDashboardController(ICatalogDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<CatalogDashboardSummaryDto>> GetSummary([FromQuery] Guid businessId)
        {
            var summary = await _dashboardService.GetDashboardSummaryAsync(businessId);
            return Ok(summary);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopClickedProducts([FromQuery] Guid businessId, [FromQuery] int topN = 5)
        {
            var topProducts = await _dashboardService.GetTopClickedProductsAsync(businessId, topN);
            return Ok(topProducts);
        }
        [HttpGet("cta-summary")]
        public async Task<IActionResult> GetCtaJourneySummary([FromQuery] Guid businessId)
        {
            var stats = await _dashboardService.GetCtaJourneyStatsAsync(businessId);
            return Ok(stats);
        }
        [HttpGet("product-cta-breakdown")]
        public async Task<IActionResult> GetProductCtaBreakdown([FromQuery] Guid businessId)
        {
            var breakdown = await _dashboardService.GetProductCtaBreakdownAsync(businessId);
            return Ok(breakdown);
        }

    }
}