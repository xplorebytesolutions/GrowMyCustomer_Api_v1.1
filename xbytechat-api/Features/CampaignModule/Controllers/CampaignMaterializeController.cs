// File: Features/CampaignModule/Controllers/CampaignMaterializeController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

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
        /// CSV-based materialization. Use Persist=false for dry-run preview; Persist=true to commit Audience + CSV recipients.
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

                Guid businessId;
                try { businessId = User.GetBusinessId(); }
                catch { return Unauthorized(); }

                var actor = ResolveActor();

                Log.Information("Materialize request: campaign={CampaignId} persist={Persist} batch={BatchId} audience='{Audience}'",
                    campaignId, dto.Persist, dto.CsvBatchId, dto.AudienceName);

                var result = await _csvMaterializer.CreateAsync(businessId, campaignId, dto, actor, ct);

                Log.Information("Materialize result: campaign={CampaignId} materialized={Count} skipped={Skipped} audienceId={AudienceId}",
                    campaignId, result.MaterializedCount, result.SkippedCount, result.AudienceId);

                return Ok(result);
            }
            catch (CampaignAudienceAttachmentService.AudienceLockedException ex)
            {
                return Conflict(new { message = ex.Message });
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
                Guid businessId;
                try { businessId = User.GetBusinessId(); }
                catch { return Unauthorized(); }

                var result = await _recipientPreview.MaterializeAsync(businessId, campaignId, limit, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Recipient preview failed for Campaign {CampaignId}", campaignId);
                return Problem(title: "Recipient preview failed", detail: ex.Message, statusCode: 400);
            }
        }

        private string ResolveActor()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type.EndsWith("email", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(email)) return email!;

            return User.Identity?.Name
                   ?? (User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value)
                   ?? "system";
        }
    }
}
