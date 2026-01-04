using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AccessControl.BusinessRolePermissions.DTOs;
using xbytechat.api.Features.AccessControl.BusinessRolePermissions.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.AccessControl.BusinessRolePermissions.Controllers
{
    [ApiController]
    [Route("api/accesscontrol/business-roles/{roleId:guid}/permissions")]
    [Authorize]
    //[Authorize(Policy = Policies.AdminOrOwner)]
    public sealed class BusinessRolePermissionsController : ControllerBase
    {
        private readonly IBusinessRolePermissionsService _service;

        public BusinessRolePermissionsController(IBusinessRolePermissionsService service)
        {
            _service = service;
        }

        private Guid GetBusinessId()
        {
            var raw =
                User.FindFirstValue("BusinessId") ??
                User.FindFirstValue("businessId") ??
                User.FindFirstValue("bid");

            if (Guid.TryParse(raw, out var id)) return id;

            throw new UnauthorizedAccessException("BusinessId claim missing.");
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromRoute] Guid roleId)
        {
            var businessId = GetBusinessId();

            try
            {
                var res = await _service.GetAsync(businessId, roleId);
                return Ok(ResponseResult.SuccessInfo("✅ Role permissions fetched.", res));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(ResponseResult.ErrorInfo("Role not found."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ResponseResult.ErrorInfo(ex.Message));
            }
        }

        [HttpPut]
        public async Task<IActionResult> Replace([FromRoute] Guid roleId, [FromBody] UpdateBusinessRolePermissionsDto dto)
        {
            var businessId = GetBusinessId();

            try
            {
                var res = await _service.ReplaceAsync(businessId, roleId, dto);
                return Ok(ResponseResult.SuccessInfo("✅ Role permissions updated.", res));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(ResponseResult.ErrorInfo("Role not found."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ResponseResult.ErrorInfo(ex.Message));
            }
        }
    }
}

