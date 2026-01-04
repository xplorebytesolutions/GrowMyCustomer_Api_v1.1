using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TeamStaff.DTOs;
using xbytechat.api.Features.TeamStaff.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.TeamStaff.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    //[Authorize(Policy = Policies.AdminOrOwner)]
    public sealed class TeamStaffController : ControllerBase
    {
        private readonly ITeamStaffService _service;

        public TeamStaffController(ITeamStaffService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetStaff()
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var rows = await _service.GetStaffAsync(businessId.Value);
            return Ok(ResponseResult.SuccessInfo("✅ Staff list fetched.", rows));
        }

        [HttpPost]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffUserDto dto)
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ UserId missing in token."));

            var result = await _service.CreateStaffAsync(businessId.Value, actorUserId.Value, dto);
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateStaff([FromRoute] Guid id, [FromBody] UpdateStaffUserDto dto)
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ UserId missing in token."));

            var result = await _service.UpdateStaffAsync(businessId.Value, actorUserId.Value, id, dto);
            return Ok(result);
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ UserId missing in token."));

            var result = await _service.SetStatusAsync(businessId.Value, actorUserId.Value, id, "Active");
            return Ok(result);
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ UserId missing in token."));

            var result = await _service.SetStatusAsync(businessId.Value, actorUserId.Value, id, "Hold");
            return Ok(result);
        }

        // ---- helpers ----

        private Guid? GetBusinessId()
        {
            // You store both "BusinessId" and "businessId" in JWT.
            var raw = User.FindFirstValue("BusinessId") ?? User.FindFirstValue("businessId");
            if (Guid.TryParse(raw, out var id)) return id;
            return null;
        }

        private Guid? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            if (Guid.TryParse(raw, out var id)) return id;
            return null;
        }
        [HttpGet("roles")]
        public async Task<IActionResult> GetAssignableRoles()
        {
            var businessId = GetBusinessId();
            if (!businessId.HasValue)
                return Unauthorized(ResponseResult.ErrorInfo("❌ BusinessId missing in token."));

            var rows = await _service.GetAssignableRolesAsync(businessId.Value);
            return Ok(ResponseResult.SuccessInfo("✅ Roles fetched.", rows));
        }


    }
}
