using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.BusinessModule.Services;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;
using static xbytechat.api.Features.MessagesEngine.Controllers.MessageEngineController;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;
        private readonly IBusinessService _businessService;
        private readonly IMessageEngineService _messageService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CampaignController(
            ICampaignService campaignService,
            IBusinessService businessService,
            IMessageEngineService messageEngineService,
            IHttpContextAccessor httpContextAccessor)
        {
            _campaignService = campaignService;
            _businessService = businessService;
            _messageService = messageEngineService;
            _httpContextAccessor = httpContextAccessor;
        }


        [HttpGet("get-image-campaign")]
        public async Task<IActionResult> GetAll([FromQuery] string? type)
        {
            var businessId = GetBusinessId();
            var items = await _campaignService.GetAllCampaignsAsync(businessId, type);
            return Ok(items);
        }
    

        [HttpPost("create-text-campaign")]
        public async Task<IActionResult> CreateTextCampaign([FromBody] CampaignCreateDto dto)
        {
            try
            {
                var businessIdClaim = User.FindFirst("businessId")?.Value;
                if (!Guid.TryParse(businessIdClaim, out var businessId))
                    return Unauthorized(new { message = "🚫 Invalid or missing BusinessId claim." });

                var createdBy = User.Identity?.Name ?? "system";

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return BadRequest(new { message = "🚫 Campaign name is required." });

                if (string.IsNullOrWhiteSpace(dto.TemplateId))
                    return BadRequest(new { message = "🚫 TemplateId is required for template campaigns." });

            

                var campaignId = await _campaignService.CreateTextCampaignAsync(dto, businessId, createdBy);

                return campaignId != null
                    ? Ok(new
                    {
                        success = true,
                        message = "✅ Campaign created successfully",
                        campaignId = campaignId.Value
                    })
                    : BadRequest(new { success = false, message = "❌ Failed to create campaign" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception in CreateTextCampaign");
                return StatusCode(500, new { message = "🚨 Internal server error", error = ex.Message });
            }
        }

        [HttpPost("create-image-campaign")]
        public async Task<IActionResult> CreateImageCampaign([FromBody] CampaignCreateDto dto)
        {
            try
            {
                var user = HttpContext.User;
                var businessIdClaim = user.FindFirst("businessId");

                if (businessIdClaim == null || !Guid.TryParse(businessIdClaim.Value, out var businessId))
                    return Unauthorized(new { message = "🚫 Invalid or missing BusinessId claim." });

                if (dto.MultiButtons != null && dto.MultiButtons.Any())
                {
                    var allowedTypes = new[] { "url", "copy_code", "flow", "phone_number", "quick_reply" };
                    foreach (var button in dto.MultiButtons)
                    {
                        var type = button.ButtonType?.Trim().ToLower();

                        if (!allowedTypes.Contains(type))
                            return BadRequest(new { message = $"❌ Invalid ButtonType: '{type}' is not supported." });

                        var needsValue = new[] { "url", "flow", "copy_code", "phone_number" };
                        if (needsValue.Contains(type) && string.IsNullOrWhiteSpace(button.TargetUrl))
                            return BadRequest(new { message = $"❌ Button '{button.ButtonText}' requires a valid TargetUrl or Value for type '{type}'." });

                        if (button.TargetUrl?.ToLower() == "unknown")
                            return BadRequest(new { message = $"❌ Invalid value 'unknown' found in button '{button.ButtonText}'." });
                    }
                }

                var createdBy = user.Identity?.Name ?? "system";
                var campaignId = await _campaignService.CreateImageCampaignAsync(businessId, dto, createdBy);

                return Ok(new
                {
                    success = true,
                    message = "✅ Campaign created successfully",
                    campaignId
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception in CreateImageCampaign");
                return StatusCode(500, new { message = "🚨 Internal server error", error = ex.Message });
            }
        }

        // ✅ Moved above {id} routes
        [HttpPost("{id}/assign-contacts")]
        public async Task<IActionResult> AssignContactsToCampaign(Guid id, [FromBody] AssignContactsDto request)
        {
            try
            {
                var businessId = GetBusinessId();
                var success = await _campaignService.AssignContactsToCampaignAsync(id, businessId, request.ContactIds);

                return success
                    ? Ok(new { message = "✅ Contacts assigned" })
                    : BadRequest(new { message = "❌ Failed to assign contacts" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error assigning contacts: " + ex.Message);
                return StatusCode(500, new { message = "Internal error", error = ex.Message });
            }
        }

        [HttpDelete("{campaignId}/recipients/{contactId}")]
        public async Task<IActionResult> RemoveCampaignRecipient(Guid campaignId, Guid contactId)
        {
            try
            {
                var businessId = GetBusinessId();
                var success = await _campaignService.RemoveRecipientAsync(businessId, campaignId, contactId);

                if (!success)
                    return NotFound(new { message = "Recipient not found or not assigned" });

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Remove recipient failed: " + ex.Message);
                return StatusCode(500, new { message = "Error removing recipient", detail = ex.Message });
            }
        }

        // Put this inside CampaignService (same class as SendTemplateCampaignWithTypeDetectionAsync)
        private static string? ResolveRecipientPhone(CampaignRecipient r)
        {
            // Try Contact first, then AudienceMember fallbacks
            return r?.Contact?.PhoneNumber
                ?? r?.AudienceMember?.PhoneE164
                ?? r?.AudienceMember?.PhoneRaw;
        }

    
        // Send All Type of campaign method 

        [Authorize]
        [HttpPost("send-campaign/{campaignId}")]
        //[ProducesResponseType(typeof(ResponseResult), StatusCodes.Status202Accepted)]
        //[ProducesResponseType(typeof(ResponseResult), StatusCodes.Status400BadRequest)]
        //[ProducesResponseType(typeof(ResponseResult), StatusCodes.Status401Unauthorized)]
        //[ProducesResponseType(typeof(ResponseResult), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendTemplateCampaign(Guid campaignId, CancellationToken ct)
        {
            using var _ = LogContext.PushProperty("CampaignId", campaignId);

            try
            {
                     var result = await _campaignService.SendTemplateCampaignWithTypeDetectionAsync(campaignId, ct);

                if (result.Success) return Accepted(result);

                // For now: any non-success from the service is treated as a client error
                return BadRequest(result);
            }
            catch (OperationCanceledException)
            {
                Log.Warning("⛔ SendTemplateCampaign cancelled");
                return StatusCode(StatusCodes.Status499ClientClosedRequest,
                    ResponseResult.ErrorInfo("Request cancelled"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception while sending campaign");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ResponseResult.ErrorInfo("🚨 Server error while sending campaign", ex.ToString()));
            }
        }



        [HttpPost("send-template-campaign/{id}")]
        public async Task<IActionResult> SendImageCampaign(Guid id)
        {
            var result = await _campaignService.SendTemplateCampaignAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("send/{campaignId}")]
        public async Task<IActionResult> SendCampaign(Guid campaignId)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers["User-Agent"].ToString() ?? "unknown";

                var success = await _campaignService.SendCampaignAsync(campaignId, ipAddress, userAgent);

                return success
                    ? Ok(new { success = true, message = "✅ Campaign sent successfully" })
                    : BadRequest(new { success = false, message = "❌ Campaign sending failed" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception in SendCampaign");
                return StatusCode(500, new { success = false, message = "🚨 Internal Server Error", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] CampaignCreateDto dto)
        {
            var result = await _campaignService.UpdateCampaignAsync(id, dto);
            return result
                ? Ok(new { message = "✏️ Campaign updated successfully" })
                : BadRequest(new { message = "❌ Update failed — only draft campaigns can be edited" });
        }



        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCampaign([FromRoute] Guid id, [FromQuery] bool force = false)
        {
            var businessId = GetBusinessId();
            var opt = new CampaignDeletionOptions { Force = force };
            var res = await _campaignService.DeleteCampaignAsync(businessId, id, opt);

            return res.Status switch
            {
                CampaignDeletionStatus.Deleted => Ok(new
                {
                    message = force
                        ? "🗑️ Campaign deleted permanently"
                        : "🗑️ Campaign deleted successfully",
                    telemetry = new
                    {
                        recipients = res.Recipients,
                        queuedJobs = res.QueuedJobs,
                        sendLogs = res.SendLogs
                    }
                }),
                CampaignDeletionStatus.BlockedSending => Conflict(new
                {
                    message = "❌ Cannot delete while campaign is sending. Cancel or wait to finish."
                }),
                CampaignDeletionStatus.BlockedState => BadRequest(new
                {
                    message = "❌ Delete failed — only draft campaigns can be deleted without force."
                }),
                CampaignDeletionStatus.NotFound => NotFound(new
                {
                    message = "❌ Campaign not found."
                }),
                _ => StatusCode(500, new
                {
                    message = "🚨 Internal error while deleting campaign."
                })
            };
        }

        [HttpGet("recipients/{id}")]
        public async Task<IActionResult> GetCampaignRecipients(Guid id)
        {
            try
            {
                var businessId = GetBusinessId();
                var recipients = await _campaignService.GetRecipientsByCampaignIdAsync(id, businessId);
                return Ok(recipients);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error fetching campaign recipients: " + ex.Message);
                return StatusCode(500, new { message = "Error fetching recipients", detail = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CampaignDto>> GetCampaignById(Guid id)
        {
            var businessId = GetBusinessId();
            var campaign = await _campaignService.GetCampaignByIdAsync(id, businessId);

            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        private Guid GetBusinessId()
        {
            var claim = HttpContext.User.FindFirst("businessId")?.Value;
            if (string.IsNullOrEmpty(claim))
                throw new UnauthorizedAccessException("BusinessId not found in token claims.");

            return Guid.Parse(claim);
        }

        [HttpGet("list/{businessId:guid}")]
        public async Task<IActionResult> GetAvailableFlows(Guid businessId, [FromQuery] bool onlyPublished = true)
        {
            var items = await _campaignService.GetAvailableFlowsAsync(businessId, onlyPublished);
            return Ok(new { success = true, items });
        }

        [HttpGet("check-name")]
        public async Task<IActionResult> CheckName([FromQuery] string name)
        {
            var businessId = GetBusinessId();
            var available = await _campaignService.CheckNameAvailableAsync(businessId, name);
            return Ok(new { available });
        }

        [HttpPut("{id:guid}/reschedule")]
        public async Task<IActionResult> Reschedule([FromRoute] Guid id, [FromBody] RescheduleDto body)
        {
            var businessId = GetBusinessId();
            await _campaignService.RescheduleAsync(businessId, id, body.NewUtcTime);
            return Ok(new { ok = true });
        }

        [HttpPost("{id:guid}/enqueue-now")]
        public async Task<IActionResult> EnqueueNow([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            await _campaignService.EnqueueNowAsync(businessId, id);
            return Ok(new { ok = true });
        }

        [HttpPost("{id:guid}/cancel-schedule")]
        public async Task<IActionResult> CancelSchedule([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            await _campaignService.CancelScheduleAsync(businessId, id);
            return Ok(new { ok = true });
        }
        [HttpGet("{id:guid}/usage")]
        public async Task<IActionResult> GetCampaignUsage([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            var usage = await _campaignService.GetCampaignUsageAsync(businessId, id);
            if (usage == null) return NotFound(new { message = "❌ Campaign not found." });
            return Ok(usage);
        }
        [HttpGet("paginated")]
        public async Task<IActionResult> GetPaginatedCampaigns([FromQuery] PaginatedRequest request)
        {
            var user = HttpContext.User;
            var businessIdClaim = user.FindFirst("businessId");

            if (businessIdClaim == null || !Guid.TryParse(businessIdClaim.Value, out var businessId))
                return Unauthorized(new { message = "🚫 Invalid or missing BusinessId claim." });

            var result = await _campaignService.GetPaginatedCampaignsAsync(businessId, request);
            return Ok(result);
        }

        [HttpGet("debug-claims")]
        public IActionResult DebugClaims()
        {
            var user = HttpContext.User;
            var businessId = user.FindFirst("businessId")?.Value;

            return Ok(new
            {
                name = user.Identity?.Name,
                businessId
            });
        }

    }
}

