// 📄 Features/AccessControl/Controllers/PermissionController.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AccessControl.DTOs;
using xbytechat.api.Features.AccessControl.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.AccessControl.Controllers
{
    [ApiController]
    [Route("api/permission")]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        // --- Existing grouped endpoint (kept for compatibility) ---
        // GET /api/permission/grouped
        [HttpGet("grouped")]
        public async Task<IActionResult> GetGroupedPermissions(CancellationToken ct)
        {
            var grouped = await _permissionService.GetGroupedPermissionsAsync();
            return Ok(ResponseResult.SuccessInfo("Permissions grouped by category", grouped));
        }

        // --- New CRUD endpoints used by PermissionsPage ---

        // GET /api/permission
        [HttpGet]
        [Authorize(Roles = "superadmin,partneradmin,admin")]
        public async Task<ActionResult<IEnumerable<PermissionSummaryDto>>> GetAll(
            CancellationToken ct)
        {
            var list = await _permissionService.GetAllAsync(ct);
            return Ok(list); // React expects a plain array
        }

        // POST /api/permission
        [HttpPost]
        [Authorize(Roles = "superadmin,partneradmin,admin")]
        public async Task<ActionResult<PermissionSummaryDto>> Create(
            [FromBody] PermissionUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var created = await _permissionService.CreateAsync(dto, ct);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/permission/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "superadmin,partneradmin,admin")]
        public async Task<ActionResult<PermissionSummaryDto>> Update(
            Guid id,
            [FromBody] PermissionUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updated = await _permissionService.UpdateAsync(id, dto, ct);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /api/permission/{id}  (soft delete)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "superadmin,partneradmin,admin")]
        public async Task<IActionResult> Deactivate(
            Guid id,
            CancellationToken ct)
        {
            try
            {
                await _permissionService.DeactivateAsync(id, ct);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
