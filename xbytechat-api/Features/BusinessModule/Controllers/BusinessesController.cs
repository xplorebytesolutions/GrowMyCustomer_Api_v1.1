using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Helpers;
using Serilog;
using System.Security.Claims;
using xbytechat.api.Features.BusinessModule.DTOs;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.BusinessModule.Services;
using Microsoft.AspNetCore.Authorization;

namespace xbytechat.api.Features.BusinessModule.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessesController : ControllerBase
    {
        private readonly IBusinessService _businessService;

        public BusinessesController(IBusinessService businessService)
        {
            _businessService = businessService;
        }



        [HttpGet("pending")]
        [Authorize(Roles = "admin,superadmin,partner")] // partners see scoped; admin/superadmin see ALL
        public async Task<IActionResult> GetPendingBusinesses([FromQuery] string? status = null)
        {
            try
            {
                // Read role/id from multiple possible claim types
                var role = User.FindFirst(ClaimTypes.Role)?.Value
                           ?? User.FindFirst("role")?.Value
                           ?? User.FindFirst("roles")?.Value
                           ?? "";

                var userId = User.FindFirst("id")?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? "";

                var result = await _businessService.GetPendingBusinessesAsync(role, userId, status);
                return Ok(ResponseResult.SuccessInfo("✅ Pending businesses fetched successfully.", result));
            }
            catch
            {
                return StatusCode(500, ResponseResult.ErrorInfo("❌ Failed to fetch pending businesses. Please try again later."));
            }
        }


        // ✅ Get business by ID (used for profile completion)

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBusinessById(Guid id)
        {
            try
            {
                var business = await _businessService.GetByIdAsync(id);
                if (business == null)
                    return NotFound(ResponseResult.ErrorInfo("❌ Business not found."));

                return Ok(business);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("🚨 Failed to fetch business. " + ex.Message));
            }
        }

        //[HttpPut("assigned-to/{id}")]
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateBusiness(Guid id, [FromBody] UpdateBusinessDto dto)
        {
            if (dto == null)
                return BadRequest(ResponseResult.ErrorInfo("❌ Invalid payload."));

            var result = await _businessService.UpdateBusinessAsync(id, dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // 🟢 Approve a business
        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(Guid id)
        {
            try
            {
                var result = await _businessService.ApproveBusinessAsync(id);

                if (result.Success)
                {
                    // ✅ Optional Success Logging
                    Log.Information("✅ Business approved successfully. BusinessId: {BusinessId}", id);
                    return Ok(result);
                }
                else
                {
                    // ✅ Optional Warning Logging
                    Log.Warning("⚠️ Business approval failed. BusinessId: {BusinessId} - Message: {Message}", id, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                // ✅ Proper Error Logging
                Log.Error(ex, "❌ Exception occurred while approving business. BusinessId: {BusinessId}", id);

                return StatusCode(500, ResponseResult.ErrorInfo(
                    "❌ Something went wrong while approving business. Please try again later."
                ));
            }
        }


        // 🔴 Reject a business
        [HttpPost("reject/{id}")]
        public async Task<IActionResult> Reject(Guid id)
        {
            try
            {
                var result = await _businessService.RejectBusinessAsync(id);
                return result.Success ? Ok(result) : NotFound(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("❌ Failed to reject business. " + ex.Message));
            }
        }

        // 🟡 Put a business on hold
        [HttpPost("hold/{id}")]
        public async Task<IActionResult> Hold(Guid id)
        {
            try
            {
                var result = await _businessService.HoldBusinessAsync(id);
                return result.Success ? Ok(result) : NotFound(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("❌ Failed to hold business. " + ex.Message));
            }
        }

        // 🟢 Assign Plan Endpoint
        [HttpPut("{id}/plan")]
        [Authorize(Roles = "superadmin,admin,partner")]
        public async Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignPlanDto dto)
        {
            if (dto == null || dto.PlanId == Guid.Empty)
                return BadRequest(ResponseResult.ErrorInfo("❌ Invalid plan ID."));

            var result = await _businessService.AssignPlanAsync(id, dto.PlanId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // 🛠 Complete profile after signup
        [HttpPost("profile-completion/{businessId}")]
        public async Task<IActionResult> CompleteProfile(Guid businessId, [FromBody] ProfileCompletionDto dto)
        {
            try
            {
                var result = await _businessService.CompleteProfileAsync(businessId, dto);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("❌ Failed to update profile. " + ex.Message));
            }
        }

        [HttpGet("approved")]
        [Authorize(Roles = "superadmin,admin,partner,reseller")]
        public async Task<IActionResult> GetApprovedBusinesses()
        {
            var result = await _businessService.GetApprovedBusinessesAsync();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "superadmin,admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _businessService.HardDeleteBusinessAsync(id);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("❌ Failed to delete business. " + ex.Message));
            }
        }

    }
}
