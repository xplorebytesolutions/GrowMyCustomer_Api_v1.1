using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/audience")]
    [Authorize]
    public sealed class CampaignAudienceController : ControllerBase
    {
        private readonly ICampaignAudienceAttachmentService _svc;

        public CampaignAudienceController(ICampaignAudienceAttachmentService svc)
        {
            _svc = svc;
        }

        [HttpGet]
        public async Task<ActionResult<CampaignAudienceDto>> GetActive([FromRoute] Guid campaignId, CancellationToken ct)
        {
            try
            {
                var businessId = User.GetBusinessId();
                var dto = await _svc.GetActiveAsync(businessId, campaignId, ct);
                return Ok(dto);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Campaign not found." });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromRoute] Guid campaignId, CancellationToken ct)
        {
            try
            {
                var businessId = User.GetBusinessId();
                var list = await _svc.GetHistoryAsync(businessId, campaignId, ct);
                return Ok(list);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Campaign not found." });
            }
        }

        public sealed class ReplaceForm
        {
            public IFormFile File { get; set; } = default!;
            public string? AudienceName { get; set; }
        }

        [HttpPost("replace")]
        [RequestSizeLimit(1024L * 1024L * 200L)] // 200 MB
        public async Task<IActionResult> Replace([FromRoute] Guid campaignId, [FromForm] ReplaceForm form, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var actor = ResolveActor();

            try
            {
                var res = await _svc.ReplaceAsync(businessId, campaignId, form.File, form.AudienceName, actor, ct);
                return Ok(res);
            }
            catch (CampaignAudienceAttachmentService.AudienceLockedException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Campaign not found." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Replace CSV audience failed | campaign={CampaignId}", campaignId);
                return Problem(title: "Replace CSV audience failed", detail: ex.Message, statusCode: 400);
            }
        }

        [HttpPost("remove")]
        public async Task<IActionResult> Remove([FromRoute] Guid campaignId, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var actor = ResolveActor();

            try
            {
                var res = await _svc.RemoveAsync(businessId, campaignId, actor, ct);
                return Ok(res);
            }
            catch (CampaignAudienceAttachmentService.AudienceLockedException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Campaign not found." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Remove CSV audience failed | campaign={CampaignId}", campaignId);
                return Problem(title: "Remove CSV audience failed", detail: ex.Message, statusCode: 400);
            }
        }

        private string ResolveActor()
        {
            // Used for CampaignAudienceAttachments.DeactivatedBy audit trail.
            var email = User.Claims.FirstOrDefault(c => c.Type.EndsWith("email", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(email)) return email!;

            return User.Identity?.Name
                   ?? (User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value)
                   ?? "system";
        }
    }
}
