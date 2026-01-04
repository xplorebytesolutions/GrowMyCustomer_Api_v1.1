using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AccessControl.BusinessRoles.DTOs;
using xbytechat.api.Features.AccessControl.BusinessRoles.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.AccessControl.BusinessRoles.Controllers
{

    [ApiController]
    [Route("api/accesscontrol/business-roles")]
    [Authorize]
    //[Authorize(Policy = Policies.AdminOrOwner)]
    public sealed class BusinessRolesController : ControllerBase
    {
        private readonly IBusinessRoleService _service;

        public BusinessRolesController(IBusinessRoleService service)
        {
            _service = service;
        }

        // ✅ IMPORTANT:
        // Replace this with your existing "get businessId from claims" helper if you already have one.
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
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var businessId = GetBusinessId();
            var rows = await _service.GetAllAsync(businessId, includeInactive);
            return Ok(ResponseResult.SuccessInfo("✅ Roles fetched.", rows));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();
            var row = await _service.GetByIdAsync(businessId, id);
            if (row == null)
                return NotFound(ResponseResult.ErrorInfo("Role not found."));
            return Ok(ResponseResult.SuccessInfo("✅ Role fetched.", row));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BusinessRoleCreateDto dto)
        {
            var businessId = GetBusinessId();

            try
            {
                var created = await _service.CreateAsync(businessId, dto);
                return Ok(ResponseResult.SuccessInfo("✅ Role created.", created));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ResponseResult.ErrorInfo(ex.Message));
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] BusinessRoleUpdateDto dto)
        {
            var businessId = GetBusinessId();

            try
            {
                var updated = await _service.UpdateAsync(businessId, id, dto);
                return Ok(ResponseResult.SuccessInfo("✅ Role updated.", updated));
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

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid id)
        {
            var businessId = GetBusinessId();

            try
            {
                await _service.DeactivateAsync(businessId, id);
                return Ok(ResponseResult.SuccessInfo("✅ Role deactivated.", null));
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
