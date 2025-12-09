// File: Features/CampaignModule/Controllers/CampaignMaterializeController.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/materialize")]
    [Authorize]
    public class CampaignMaterializeController : ControllerBase
    {
        private readonly ICampaignMaterializer _csvMaterializer;
        private readonly ICampaignMaterializationService _recipientPreview;

        public CampaignMaterializeController(
            ICampaignMaterializer csvMaterializer,
            ICampaignMaterializationService recipientPreview)
        {
            _csvMaterializer = csvMaterializer;
            _recipientPreview = recipientPreview;
        }

        /// <summary>
        /// CSV-based materialization. Use Persist=false for dry-run preview; Persist=true to commit Audience + Recipients.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CampaignCsvMaterializeResponseDto>> CsvCreate(
            [FromRoute] Guid campaignId,
            [FromBody] CampaignCsvMaterializeRequestDto dto,
            CancellationToken ct)
        {
            try
            {
                if (dto is null) return BadRequest("Body required.");

                var businessId = ResolveBusinessId();
                Log.Information("📦 Materialize request: campaign={CampaignId} persist={Persist} batch={BatchId} audience='{Audience}'",
                    campaignId, dto.Persist, dto.CsvBatchId, dto.AudienceName);

                var result = await _csvMaterializer.CreateAsync(businessId, campaignId, dto, ct);

                Log.Information("📦 Materialize result: campaign={CampaignId} materialized={Count} skipped={Skipped} audienceId={AudienceId}",
                    campaignId, result.MaterializedCount, result.SkippedCount, result.AudienceId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CSV materialize failed for Campaign {CampaignId}", campaignId);
                return Problem(title: "CSV materialize failed", detail: ex.Message, statusCode: 400);
            }
        }

        /// <summary>
        /// Recipient-based preview (read-only), using existing recipients + contacts.
        /// </summary>
        [HttpGet("recipients")]
        public async Task<ActionResult<CampaignMaterializeResultDto>> RecipientPreview(
            [FromRoute] Guid campaignId,
            [FromQuery] int limit = 200,
            CancellationToken ct = default)
        {
            try
            {
                var businessId = ResolveBusinessId();
                var result = await _recipientPreview.MaterializeAsync(businessId, campaignId, limit, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Recipient preview failed for Campaign {CampaignId}", campaignId);
                return Problem(title: "Recipient preview failed", detail: ex.Message, statusCode: 400);
            }
        }

        private Guid ResolveBusinessId()
        {
            var bidStr = User.FindFirst("BusinessId")?.Value
                         ?? Request.Headers["X-Business-Id"].ToString();
            return Guid.TryParse(bidStr, out var bid) ? bid : Guid.Empty;
        }
    }
}
