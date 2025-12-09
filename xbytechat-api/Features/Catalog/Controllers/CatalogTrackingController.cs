using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Services;
using xbytechat.api.Helpers;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.Catalog.Controllers
{
    [ApiController]
    [Route("api/catalog-tracking")]
    public class CatalogTrackingController : ControllerBase
    {
        private readonly ICatalogTrackingService _trackingService;
        private readonly ILogger<CatalogTrackingController> _logger;

        public CatalogTrackingController(
            ICatalogTrackingService trackingService,
            ILogger<CatalogTrackingController> logger)
        {
            _trackingService = trackingService;
            _logger = logger;
        }

        [HttpPost("log-click")]
        public async Task<IActionResult> LogClick([FromBody] CatalogClickLogDto dto)
        {
            var result = await _trackingService.LogClickAsync(dto);

            if (!result.Success)
            {
                _logger.LogWarning("❌ Catalog click log failed: {Msg}", result.Message);
                return BadRequest(result);
            }

            return StatusCode(201, result);
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentLogs([FromQuery] int limit = 5)
        {
            var result = await _trackingService.GetRecentLogsAsync(limit);

            if (!result.Success)
            {
                _logger.LogError("❌ Failed to fetch recent logs: {Error}", result.ErrorMessage);
                return StatusCode(500, result);
            }

            return Ok(result);
        }
    }
}
