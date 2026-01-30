using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Services;

namespace xbytechat.api.Features.CTAFlowBuilder.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/cta-flow")]
    public class CTAFlowController : ControllerBase
    {
        private readonly ICTAFlowService _flowService;

        public CTAFlowController(ICTAFlowService flowService)
        {
            _flowService = flowService;
        }

        // CREATE (draft-only)
        [HttpPost("save-visual")]
        public async Task<IActionResult> SaveVisualFlow([FromBody] SaveVisualFlowDto dto)
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            var createdBy = User.FindFirst("name")?.Value ?? "system";
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest(new { message = "❌ Invalid business ID" });

            Log.Information("📦 Saving CTA Flow: {FlowName} by {User}", dto.FlowName, createdBy);

            var result = await _flowService.SaveVisualFlowAsync(dto, businessId, createdBy);
            if (!result.Success)
            {
                var m = (result.ErrorMessage ?? "").Trim();

                // map common validation/conflict by message text (no result.Code available)
                if (m.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { message = "❌ Duplicate flow name", error = m });

                if (m.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("empty flow", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "❌ Failed to save flow", error = m });

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "❌ Failed to save flow", error = string.IsNullOrWhiteSpace(m) ? "Unknown error" : m });
            }

            Guid? flowId = null;
            if (result.Data is not null)
            {
                try { dynamic d = result.Data; flowId = (Guid?)d.flowId; } catch { }
            }

            return Ok(new { message = "✅ Flow saved successfully", flowId });
        }

        // UPDATE (save as draft by id)
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveVisualFlowDto dto)
        {
            var biz = User.FindFirst("businessId")?.Value;
            var user = User.FindFirst("name")?.Value ?? "system";
            if (!Guid.TryParse(biz, out var businessId))
                return BadRequest(new { message = "❌ Invalid business." });

            var result = await _flowService.UpdateVisualFlowAsync(id, dto, businessId, user);

            return result.Status switch
            {
                "notFound" => NotFound(new { message = result.Message ?? "❌ Flow not found." }),
                "requiresFork" => Conflict(new { message = result.Message ?? "❌ Edit requires fork.", campaigns = result.Campaigns }),
                "error" => BadRequest(new { message = result.Message ?? "❌ Failed to update flow." }),
                _ => Ok(new { message = result.Message ?? "✅ Flow updated (draft).", needsRepublish = result.NeedsRepublish })
            };
        }

        // FORK (create a new draft copy)
        [HttpPost("{id:guid}/fork")]
        public async Task<IActionResult> Fork(Guid id)
        {
            var biz = User.FindFirst("businessId")?.Value;
            var user = User.FindFirst("name")?.Value ?? "system";
            if (!Guid.TryParse(biz, out var businessId))
                return BadRequest(new { message = "❌ Invalid business." });

            var forkId = await _flowService.ForkFlowAsync(id, businessId, user);
            if (forkId == Guid.Empty) return NotFound(new { message = "❌ Flow not found." });

            return Ok(new { flowId = forkId });
        }

        // PUBLISH (by id)
        [HttpPost("{id:guid}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            var biz = User.FindFirst("businessId")?.Value;
            var user = User.FindFirst("name")?.Value ?? "system";
            if (!Guid.TryParse(biz, out var businessId))
                return BadRequest(new { message = "❌ Invalid business." });

            var result = await _flowService.PublishFlowAsync(id, businessId, user);

            if (result.Success)
                return Ok(new { message = result.Message ?? "✅ Flow published." });

            var msg = (result.ErrorMessage ?? result.Message ?? "❌ Failed to publish.").Trim();

            if (result.Code == 404)
                return NotFound(new { message = msg });

            // Validation failures return 400 with details in payload (issue list)
            if (result.Code == 400)
                return BadRequest(new { message = msg, issues = result.Payload });

            return BadRequest(new { message = msg });
        }

        // DELETE (only if not attached)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var biz = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(biz, out var businessId))
                return BadRequest(new { message = "❌ Invalid business." });

            var deletedBy = User.FindFirst("name")?.Value
                          ?? User.FindFirst("email")?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? "system";

            var result = await _flowService.DeleteFlowAsync(id, businessId, deletedBy);

            if (!result.Success)
            {
                var msg = (result.ErrorMessage ?? result.Message ?? string.Empty).Trim();

                // If message says it's attached, return 409 and include campaigns for the modal
                if (msg.Contains("attached", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("Cannot delete", StringComparison.OrdinalIgnoreCase))
                {
                    var campaigns = await _flowService.GetAttachedCampaignsAsync(id, businessId);
                    return Conflict(new { message = msg, campaigns });
                }

                if (msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { message = msg });

                return BadRequest(new { message = string.IsNullOrWhiteSpace(msg) ? "Delete failed." : msg });
            }

            return Ok(new { message = result.Message ?? "✅ Flow deleted." });
        }

        // LISTS
        [HttpGet("all-published")]
        public async Task<IActionResult> GetPublishedFlows()
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest(new { message = "❌ Invalid business ID" });

            var flows = await _flowService.GetAllPublishedFlowsAsync(businessId);
            return Ok(flows);
        }

        [HttpGet("all-draft")]
        public async Task<IActionResult> GetAllDraftFlows()
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest(new { message = "❌ Invalid business ID" });

            var flows = await _flowService.GetAllDraftFlowsAsync(businessId);
            return Ok(flows);
        }

        // DETAIL
        [HttpGet("by-id/{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest(new { message = "❌ Invalid business ID" });

            var dto = await _flowService.GetVisualFlowByIdAsync(id, businessId);
            if (dto is null) return NotFound(new { message = "❌ Flow not found." });

            return Ok(dto);
        }

        [HttpGet("visual/{id:guid}")]
        public async Task<IActionResult> GetVisualFlow(Guid id)
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest(new { message = "❌ Invalid business ID" });

            var result = await _flowService.GetVisualFlowAsync(id, businessId);
            if (!result.Success)
            {
                var m = (result.ErrorMessage ?? string.Empty).Trim();
                if (m.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { message = "❌ Failed to load flow", error = m });

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "❌ Failed to load flow", error = string.IsNullOrWhiteSpace(m) ? "Unknown error" : m });
            }

            return Ok(result.Data);
        }

        // USAGE (for delete guard)
        [HttpGet("{id:guid}/usage")]
        public async Task<IActionResult> GetUsage(Guid id)
        {
            var biz = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(biz, out var businessId))
                return BadRequest(new { message = "Invalid business." });

            var campaigns = await _flowService.GetAttachedCampaignsAsync(id, businessId);
            return Ok(new
            {
                canDelete = campaigns.Count == 0,
                count = campaigns.Count,
                campaigns
            });
        }
    }
}

