using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Services;

namespace xbytechat.api.Features.CTAFlowBuilder.Controllers
{
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


//// 📄 File: Features/CTAFlowBuilder/Controllers/CTAFlowController.cs
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Serilog;
//using xbytechat.api.Features.CTAFlowBuilder.DTOs;
//using xbytechat.api.Features.CTAFlowBuilder.Models;
//using xbytechat.api.Features.CTAFlowBuilder.Services;
//using xbytechat.api.Features.MessagesEngine.DTOs;
//using xbytechat.api.Features.MessagesEngine.Services;
//using xbytechat.api.Features.Tracking.Models;
//using xbytechat.api.Features.Tracking.Services;
//using xbytechat.api.Helpers;
//using xbytechat.api.Shared;


//namespace xbytechat.api.Features.CTAFlowBuilder.Controllers
//{
//    [ApiController]
//    [Route("api/cta-flow")]
//    public class CTAFlowController : ControllerBase
//    {
//        private readonly ICTAFlowService _flowService;
//        private readonly IMessageEngineService _messageEngineService;
//        private readonly ITrackingService _trackingService;
//        public CTAFlowController(ICTAFlowService flowService, IMessageEngineService messageEngineService, ITrackingService trackingService)
//        {
//            _flowService = flowService;
//            _messageEngineService = messageEngineService;
//            _trackingService = trackingService;
//        }

//        [HttpPost("create")]
//        public async Task<IActionResult> CreateFlow([FromBody] CreateFlowDto dto)
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            var createdBy = User.FindFirst("name")?.Value ?? "system";

//            if (string.IsNullOrWhiteSpace(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid or missing businessId claim.");

//            var id = await _flowService.CreateFlowWithStepsAsync(dto, businessId, createdBy);
//            return Ok(new { flowId = id });
//        }

//        //[HttpPost("publish")]
//        //public async Task<IActionResult> PublishFlow([FromBody] List<FlowStepDto> steps)
//        //{
//        //    var businessIdClaim = User.FindFirst("businessId")?.Value;
//        //    var createdBy = User.FindFirst("name")?.Value ?? "system";

//        //    if (string.IsNullOrWhiteSpace(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
//        //        return BadRequest("❌ Invalid or missing businessId claim.");

//        //    var result = await _flowService.PublishFlowAsync(businessId, steps, createdBy);
//        //    if (!result.Success)
//        //        return BadRequest(result.Message);

//        //    return Ok("✅ Flow published successfully.");
//        //}

//        [HttpGet("current")]
//        public async Task<IActionResult> GetFlow()
//        {
//            var businessIdHeader = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdHeader, out var businessId))
//                return BadRequest("❌ Invalid or missing BusinessId header.");

//            var flow = await _flowService.GetFlowByBusinessAsync(businessId);

//            // ✅ Always return 200 even if flow is null
//            return Ok(flow);
//        }

//        [HttpGet("draft")]
//        public async Task<IActionResult> GetDraftFlow()
//        {
//            var businessIdHeader = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdHeader, out var businessId))
//                return BadRequest("❌ Invalid or missing BusinessId header.");

//            var draft = await _flowService.GetDraftFlowByBusinessAsync(businessId);
//            if (draft == null)
//                return NotFound("❌ No draft flow found.");

//            return Ok(draft);
//        }

//        [HttpGet("match")]
//        public async Task<IActionResult> MatchButton(
//            [FromQuery] string text,
//            [FromQuery] string type,
//            [FromQuery] string currentTemplateName,
//            [FromQuery] Guid? campaignId) // Optional
//        {
//            var businessId = Guid.Parse(User.FindFirst("businessId")?.Value!);

//            var step = await _flowService.MatchStepByButtonAsync(
//                businessId,
//                text,
//                type,
//                currentTemplateName,
//                campaignId
//            );

//            if (step == null)
//                return NotFound("❌ No matching step found.");

//            return Ok(new
//            {
//                step.TemplateToSend,
//                step.TriggerButtonText,
//                step.TriggerButtonType
//            });
//        }

//        //[HttpPost("save-visual")]
//        //public async Task<IActionResult> SaveVisualFlow([FromBody] SaveVisualFlowDto dto)
//        //{
//        //    var businessIdClaim = User.FindFirst("businessId")?.Value;
//        //    var createdBy = User.FindFirst("name")?.Value ?? "system";

//        //    if (!Guid.TryParse(businessIdClaim, out var businessId))
//        //        return BadRequest("❌ Invalid business ID");

//        //    Log.Information("📦 Saving CTA Flow: {FlowName} by {User}", dto.FlowName, createdBy);

//        //    var result = await _flowService.SaveVisualFlowAsync(dto, businessId, createdBy);
//        //    if (!result.Success)
//        //    {
//        //        Log.Error("❌ Failed to save flow. Error: {Error}. DTO: {@Dto}", result.ErrorMessage, dto);
//        //        return StatusCode(500, new
//        //        {
//        //            message = "❌ Failed to save flow",
//        //            error = result.ErrorMessage,
//        //            // skipped = result.SkippedNodes ?? 0
//        //        });
//        //    }

//        //    return Ok(new
//        //    {
//        //        message = "✅ Flow saved successfully"
//        //    });
//        //}

//        //[HttpPost("save-visual")]
//        //public async Task<IActionResult> SaveVisualFlow([FromBody] SaveVisualFlowDto dto)
//        //{
//        //    var businessIdClaim = User.FindFirst("businessId")?.Value;
//        //    var createdBy = User.FindFirst("name")?.Value ?? "system";

//        //    if (!Guid.TryParse(businessIdClaim, out var businessId))
//        //        return BadRequest("❌ Invalid business ID");

//        //    Log.Information("📦 Saving CTA Flow: {FlowName} by {User}", dto.FlowName, createdBy);

//        //    var result = await _flowService.SaveVisualFlowAsync(dto, businessId, createdBy);
//        //    if (!result.Success)
//        //    {
//        //        Log.Error("❌ Failed to save flow. Error: {Error}. DTO: {@Dto}", result.ErrorMessage, dto);
//        //        return StatusCode(500, new
//        //        {
//        //            message = "❌ Failed to save flow",
//        //            error = result.ErrorMessage
//        //        });
//        //    }

//        //    // Try to extract flowId from the service result (supports several shapes).
//        //    Guid? flowId = null;
//        //    try
//        //    {
//        //        switch (result.Data)
//        //        {
//        //            case Guid g:
//        //                flowId = g;
//        //                break;

//        //            case string s when Guid.TryParse(s, out var gs):
//        //                flowId = gs;
//        //                break;

//        //            case { } obj:
//        //                // look for a property literally named "flowId"
//        //                var prop = obj.GetType().GetProperty("flowId")
//        //                           ?? obj.GetType().GetProperty("FlowId");
//        //                if (prop?.GetValue(obj) is Guid pg)
//        //                    flowId = pg;
//        //                else if (prop?.GetValue(obj) is string ps && Guid.TryParse(ps, out var pgs))
//        //                    flowId = pgs;
//        //                break;
//        //        }
//        //    }
//        //    catch
//        //    {
//        //        // non-fatal: just return without flowId if reflection fails
//        //    }

//        //    return Ok(new
//        //    {
//        //        message = "✅ Flow saved successfully",
//        //        flowId
//        //    });
//        //}
//        // POST /api/cta-flow/save-visual
//        // xbytechat.api/Features/CTAFlowBuilder/Controllers/CTAFlowController.cs
//        [HttpPost("save-visual")]
//        public async Task<IActionResult> SaveVisualFlow([FromBody] SaveVisualFlowDto dto)
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            var createdBy = User.FindFirst("name")?.Value ?? "system";

//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid business ID");

//            Log.Information("📦 Saving CTA Flow: {FlowName} by {User}", dto.FlowName, createdBy);

//            var result = await _flowService.SaveVisualFlowAsync(dto, businessId, createdBy);

//            // Helper: classify error message to proper HTTP status
//            IActionResult ErrorToHttp(string? msg)
//            {
//                var m = (msg ?? "").Trim();

//                // Validation problems → 400
//                if (m.Contains("Flow name is required", StringComparison.OrdinalIgnoreCase) ||
//                    m.Contains("Cannot save an empty flow", StringComparison.OrdinalIgnoreCase) ||
//                    m.Contains("invalid", StringComparison.OrdinalIgnoreCase))
//                {
//                    Log.Warning("⚠️ Validation error while saving flow: {Error}", m);
//                    return BadRequest(new { message = "❌ Failed to save flow", error = m });
//                }

//                // Duplicate name on create → 409 (frontend will open rename modal)
//                if (m.Contains("already exists", StringComparison.OrdinalIgnoreCase))
//                {
//                    Log.Warning("⚠️ Duplicate flow name when creating: {Error}", m);
//                    return StatusCode(StatusCodes.Status409Conflict, new
//                    {
//                        message = "❌ Duplicate flow name",
//                        error = m
//                        // intentionally no 'campaigns' array here (FE uses presence of it to show fork modal)
//                    });
//                }

//                // Unknown → 500
//                Log.Error("❌ Failed to save flow. Error: {Error}. DTO: {@Dto}", m, dto);
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    message = "❌ Failed to save flow",
//                    error = string.IsNullOrWhiteSpace(m) ? "Unknown error" : m
//                });
//            }

//            if (!result.Success)
//                return ErrorToHttp(result.ErrorMessage);

//            // Expect service to put { flowId = <Guid> } into result.Data
//            Guid? flowId = null;
//            if (result.Data is not null)
//            {
//                try
//                {
//                    if (result.Data is IDictionary<string, object> dict
//                        && dict.TryGetValue("flowId", out var obj) && obj is Guid g1)
//                    {
//                        flowId = g1;
//                    }
//                    else
//                    {
//                        // dynamic fallback
//                        dynamic d = result.Data;
//                        flowId = (Guid?)d.flowId;
//                    }
//                }
//                catch
//                {
//                    // ignore shape issues; flowId stays null
//                }
//            }

//            return Ok(new
//            {
//                message = "✅ Flow saved successfully",
//                flowId
//            });
//        }


//        //[HttpPost("save-visual")]
//        //public async Task<IActionResult> SaveVisualFlow([FromBody] SaveVisualFlowDto dto)
//        //{
//        //    var businessIdClaim = User.FindFirst("businessId")?.Value;
//        //    var createdBy = User.FindFirst("name")?.Value ?? "system";

//        //    if (!Guid.TryParse(businessIdClaim, out var businessId))
//        //        return BadRequest("❌ Invalid business ID");

//        //    Log.Information("📦 Saving CTA Flow: {FlowName} by {User}", dto.FlowName, createdBy);

//        //    var result = await _flowService.SaveVisualFlowAsync(dto, businessId, createdBy);
//        //    if (!result.Success)
//        //    {
//        //        Log.Error("❌ Failed to save flow. Error: {Error}. DTO: {@Dto}", result.ErrorMessage, dto);
//        //        return StatusCode(500, new
//        //        {
//        //            message = "❌ Failed to save flow",
//        //            error = result.ErrorMessage
//        //        });
//        //    }

//        //    // Expect service to put { flowId = <Guid> } into result.Data
//        //    Guid? flowId = null;
//        //    if (result.Data is not null)
//        //    {
//        //        try
//        //        {
//        //            // Support anonymous object or dictionary
//        //            var dict = result.Data as IDictionary<string, object>;
//        //            if (dict != null && dict.TryGetValue("flowId", out var obj) && obj is Guid g1)
//        //                flowId = g1;
//        //            else
//        //            {
//        //                // dynamic fallback
//        //                dynamic d = result.Data;
//        //                flowId = (Guid?)d.flowId;
//        //            }
//        //        }
//        //        catch { /* ignore shape issues; flowId stays null */ }
//        //    }

//        //    return Ok(new
//        //    {
//        //        message = "✅ Flow saved successfully",
//        //        flowId
//        //    });
//        //}

//        [HttpGet("{id:guid}/usage")]
//        public async Task<IActionResult> GetUsage(Guid id)
//        {
//            var biz = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(biz, out var businessId)) return BadRequest(new { message = "Invalid business." });

//            var campaigns = await _flowService.GetAttachedCampaignsAsync(id, businessId);
//            return Ok(new
//            {
//                canDelete = campaigns.Count == 0,
//                count = campaigns.Count,
//                campaigns
//            });
//        }

//        //[HttpDelete("{id:guid}")]
//        //public async Task<IActionResult> Delete(Guid id)
//        //{
//        //    var biz = User.FindFirst("businessId")?.Value;
//        //    if (!Guid.TryParse(biz, out var businessId)) return BadRequest(new { message = "Invalid business." });

//        //    // Try hard delete
//        //    var deleted = await _flowService.HardDeleteFlowIfUnusedAsync(id, businessId);
//        //    if (deleted) return NoContent();

//        //    // If not deleted, return 409 with who’s attached
//        //    var campaigns = await _flowService.GetAttachedCampaignsAsync(id, businessId);
//        //    if (campaigns.Count > 0)
//        //    {
//        //        return Conflict(new
//        //        {
//        //            message = "Flow is attached to the following campaign(s). Delete them first, then delete the flow.",
//        //            campaigns
//        //        });
//        //    }

//        //    // Not found (wrong tenant or already deleted)
//        //    return NotFound(new { message = "Flow not found." });
//        //}
//        // KEEP ONLY THIS ONE DELETE ENDPOINT

//        [HttpDelete("{id:guid}")]
//        public async Task<IActionResult> Delete(Guid id)
//        {
//            var biz = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(biz, out var businessId))
//                return BadRequest(new { message = "❌ Invalid business." });

//            // who is deleting (audit)
//            var deletedBy = User.FindFirst("name")?.Value
//                          ?? User.FindFirst("email")?.Value
//                          ?? User.FindFirst("sub")?.Value
//                          ?? "system";

//            // Use your service that understands attachment rules and returns codes
//            var result = await _flowService.DeleteFlowAsync(id, businessId, deletedBy);

//            // Frontend expects 409 to show the modal with attached campaigns
//            if (!result.Success && result.Code == 409)
//                return Conflict(new { message = result.Message, campaigns = result.Payload });

//            if (!result.Success && result.Code == 404)
//                return NotFound(new { message = result.Message });

//            if (!result.Success)
//                return BadRequest(new { message = result.Message });

//            // FE treats 200 or 204 as success — return 200 with a message
//            return Ok(new { message = result.Message });
//        }



//        [HttpGet("all-published")]
//        public async Task<IActionResult> GetPublishedFlows()
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid business ID");

//            var flows = await _flowService.GetAllPublishedFlowsAsync(businessId);
//            return Ok(flows);
//        }
//        [HttpGet("all-draft")]
//        public async Task<IActionResult> GetAllDraftFlows()
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid business ID");

//            var flows = await _flowService.GetAllDraftFlowsAsync(businessId);
//            return Ok(flows);
//        }


//        [HttpPost("execute-visual")]
//        public async Task<IActionResult> ExecuteVisualFlowAsync(
//            [FromQuery] Guid nextStepId,
//            [FromQuery] Guid trackingLogId,
//            // ✅ 1. ADD the new optional parameter to the endpoint
//            [FromQuery] Guid? campaignSendLogId = null)
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid business ID");

//            // ✅ 2. PASS the new parameter to the service call
//            var result = await _flowService.ExecuteVisualFlowAsync(businessId, nextStepId, trackingLogId, campaignSendLogId);

//            if (result.Success)
//                return Ok(result);
//            else
//                return BadRequest(result);
//        }

//        [HttpPost("create-config")]
//        public async Task<IActionResult> CreateConfigFlow([FromBody] CreateFlowDto dto)
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            var createdBy = User.FindFirst("name")?.Value ?? "system";

//            if (string.IsNullOrWhiteSpace(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid or missing businessId claim.");

//            try
//            {
//                var id = await _flowService.CreateFlowWithStepsAsync(dto, businessId, createdBy);

//                return Ok(new
//                {
//                    flowId = id,
//                    message = "✅ Flow config created successfully."
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    error = "❌ Failed to create flow config.",
//                    details = ex.Message
//                });
//            }
//        }

//        [HttpGet("by-id/{id:guid}")]
//        public async Task<IActionResult> GetById(Guid id)
//        {
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest("❌ Invalid business ID");

//            // Prefer a tenant-aware service method
//            var dto = await _flowService.GetVisualFlowByIdAsync(id, businessId);
//            if (dto is null) return NotFound("❌ Flow not found.");

//            return Ok(dto); // { flowName, isPublished, nodes, edges } (camelCase via default JSON options)
//        }

//        // 📍 Add this to your CTAFlowController
//        // CTAFlowController.cs  — drop-in replacement for "get visual flow" endpoint
//        [HttpGet("visual/{id:guid}")]
//        public async Task<IActionResult> GetVisualFlow(Guid id)
//        {
//            // business guard
//            var businessIdClaim = User.FindFirst("businessId")?.Value;
//            if (!Guid.TryParse(businessIdClaim, out var businessId))
//                return BadRequest(new { message = "❌ Failed to load flow", error = "Invalid business ID" });

//            // ask the service — this RETURNS YOUR ResponseResult, not a DTO
//            var result = await _flowService.GetVisualFlowAsync(id, businessId);

//            // Uniform error mapping (same style you use in SaveVisualFlow)
//            if (!result.Success)
//            {
//                var m = (result.ErrorMessage ?? string.Empty).Trim();

//                if (m.Contains("not found", StringComparison.OrdinalIgnoreCase))
//                {
//                    // 404 when flow id doesn’t exist / not visible for biz
//                    return NotFound(new { message = "❌ Failed to load flow", error = m });
//                }

//                if (m.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
//                    m.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
//                {
//                    // 403 when biz is not allowed to see this flow
//                    return StatusCode(StatusCodes.Status403Forbidden,
//                        new { message = "❌ Failed to load flow", error = m });
//                }

//                // default → 500
//                return StatusCode(StatusCodes.Status500InternalServerError,
//                    new { message = "❌ Failed to load flow", error = string.IsNullOrWhiteSpace(m) ? "Unknown error" : m });
//            }

//            // SUCCESS: your service already builds the payload (FlowName, IsPublished, Nodes, Edges, etc.)
//            // Just return it as-is so the FE can consume it.
//            // Example shape expected by FE:
//            // { flowName, isPublished, nodes: [...], edges: [...] }
//            return Ok(result.Data);
//        }


//        //[HttpPut("{id:guid}")]
//        //public async Task<IActionResult> Update(Guid id, [FromBody] SaveVisualFlowDto dto)
//        //{
//        //    var biz = User.FindFirst("businessId")?.Value;
//        //    var user = User.FindFirst("name")?.Value ?? "system";
//        //    if (!Guid.TryParse(biz, out var businessId)) return BadRequest("❌ Invalid business.");

//        //    var result = await _flowService.UpdateVisualFlowAsync(id, dto, businessId, user);
//        //    return result.Status switch
//        //    {
//        //        "ok" => Ok(new { message = "Flow updated.", needsRepublish = result.NeedsRepublish }),
//        //        "requiresFork" => Conflict(new { message = result.Message, campaigns = result.Campaigns, requiresFork = true }),
//        //        "notFound" => NotFound("❌ Flow not found."),
//        //        _ => BadRequest(new { message = result.Message ?? "Unknown error" })
//        //    };
//        //}


//        [HttpPost("{id:guid}/publish")]
//        public async Task<IActionResult> Publish(Guid id)
//        {
//            var biz = User.FindFirst("businessId")?.Value;
//            var user = User.FindFirst("name")?.Value ?? "system";
//            if (!Guid.TryParse(biz, out var businessId)) return BadRequest("❌ Invalid business.");

//            var ok = await _flowService.PublishFlowAsync(id, businessId, user);
//            return ok ? Ok(new { message = "✅ Flow published." }) : NotFound("❌ Flow not found.");
//        }


//        //// 👇 NEW: publish
//        //[HttpPost("{id:guid}/publish")]
//        //public async Task<IActionResult> Publish(Guid id)
//        //{
//        //    var biz = User.FindFirst("businessId")?.Value;
//        //    var user = User.FindFirst("name")?.Value ?? "system";
//        //    if (!Guid.TryParse(biz, out var businessId)) return BadRequest("❌ Invalid business.");

//        //    var ok = await _flowService.PublishFlowAsync(id, businessId, user);
//        //    return ok ? Ok(new { message = "✅ Flow published." }) : NotFound("❌ Flow not found.");
//        //}



//        // 👇 NEW: fork (create new draft from live-locked flow)
//        [HttpPost("{id:guid}/fork")]
//        public async Task<IActionResult> Fork(Guid id)
//        {
//            var biz = User.FindFirst("businessId")?.Value;
//            var user = User.FindFirst("name")?.Value ?? "system";
//            if (!Guid.TryParse(biz, out var businessId)) return BadRequest("❌ Invalid business.");

//            var forkId = await _flowService.ForkFlowAsync(id, businessId, user);
//            if (forkId == Guid.Empty) return NotFound("❌ Flow not found.");
//            return Ok(new { flowId = forkId });
//        }

//        // 👇 BACK-COMPAT: keep existing delete route AND add /{id}
//       // [HttpDelete("{id:guid}")]
//        //public async Task<IActionResult> DeletePlain(Guid id)
//        //{
//        //    var biz = User.FindFirst("businessId")?.Value;
//        //    if (!Guid.TryParse(biz, out var businessId))
//        //        return BadRequest("❌ Invalid business ID");

//        //    // Capture the user performing the delete
//        //    var deletedBy = User.FindFirst("name")?.Value
//        //                 ?? User.FindFirst("email")?.Value
//        //                 ?? User.FindFirst("sub")?.Value
//        //                 ?? "system";

//        //    var result = await _flowService.DeleteFlowAsync(id, businessId, deletedBy);

//        //    if (!result.Success && result.Code == 409)
//        //        return Conflict(new { message = result.Message, campaigns = result.Payload });

//        //    if (!result.Success && result.Code == 404)
//        //        return NotFound(new { message = result.Message });

//        //    if (!result.Success)
//        //        return BadRequest(new { message = result.Message });

//        //    return Ok(new { message = result.Message });
//        //}

//    }

//}
