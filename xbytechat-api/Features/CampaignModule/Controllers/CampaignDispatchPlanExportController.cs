using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/dispatch-plan.csv")]
    [Authorize]
    public class CampaignDispatchPlanExportController : ControllerBase
    {
        private readonly ICsvExportService _csv;

        public CampaignDispatchPlanExportController(ICsvExportService csv)
        {
            _csv = csv;
        }

        /// <summary>
        /// Streams a CSV of the dispatch plan (batches, offsets, recipients).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(Guid campaignId, [FromQuery] int limit = 2000, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            Log.Information("Dispatch Plan CSV requested {@Ctx}", new { businessId, campaignId, limit });

            var bytes = await _csv.BuildDispatchPlanCsvAsync(businessId, campaignId, limit, ct);
            var fileName = $"dispatch_plan_{campaignId:N}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}
