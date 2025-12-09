#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Controllers
{
    /// <summary>
    /// Manage per-user permission overrides.
    ///
    /// Important:
    /// - We do NOT calculate effective permissions here.
    ///   Plan → permissions is handled by Plan/AccessControl services.
    /// - This controller only manages rows in UserPermission (allow/deny overrides).
    ///   The UI can merge:
    ///     a) plan permissions  + 
    ///     b) these overrides
    ///   to show the final state for each feature.
    /// </summary>
    [ApiController]
    [Route("api/admin/users/{userId:guid}/permissions")]
    [Authorize(Roles = "admin")]
    public sealed class UserPermissionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UserPermissionsController(AppDbContext db)
        {
            _db = db;
        }

        // --------- DTOs (you can later move them to Features/AccessControl/DTOs) ---------

        public sealed class UserPermissionOverrideDto
        {
            public Guid PermissionId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// true  = explicit allow
            /// false = explicit deny
            /// </summary>
            public bool IsGranted { get; set; }

            /// <summary>
            /// true  = override is logically removed (soft delete)
            /// false = active override
            /// </summary>
            public bool IsRevoked { get; set; }

            public DateTime AssignedAt { get; set; }
            public string? AssignedBy { get; set; }
        }

        public sealed class UpsertUserPermissionRequest
        {
            public Guid PermissionId { get; set; }

            /// <summary>
            /// true  = allow
            /// false = deny
            /// </summary>
            public bool IsGranted { get; set; }
        }

        // ---------------- GET: list overrides for a user ----------------

        /// <summary>
        /// Returns all active overrides for the given user.
        /// The UI should combine this with plan permissions to show final state.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<UserPermissionOverrideDto>>> GetOverridesForUser(
            Guid userId,
            CancellationToken ct)
        {
            // Ensure the user exists (optional but nice for admin UX)
            var userExists = await _db.Set<User>()
                .AnyAsync(u => u.Id == userId, ct);

            if (!userExists)
            {
                return NotFound($"User {userId} not found.");
            }

            var overrides = await _db.Set<UserPermission>()
                .AsNoTracking()
                .Where(up => up.UserId == userId && !up.IsRevoked)
                .Include(up => up.Permission)
                .OrderBy(up => up.Permission.Code)
                .Select(up => new UserPermissionOverrideDto
                {
                    PermissionId = up.PermissionId,
                    Code = up.Permission.Code,
                    Name = up.Permission.Name,
                    IsGranted = up.IsGranted,
                    IsRevoked = up.IsRevoked,
                    AssignedAt = up.AssignedAt,
                    AssignedBy = up.AssignedBy
                })
                .ToListAsync(ct);

            return overrides;
        }

        // ---------------- POST: create/update override ----------------

        /// <summary>
        /// Create or update an override for the given user & permission.
        /// If row exists, we update IsGranted and clear IsRevoked.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UserPermissionOverrideDto>> UpsertOverride(
            Guid userId,
            [FromBody] UpsertUserPermissionRequest request,
            CancellationToken ct)
        {
            if (request.PermissionId == Guid.Empty)
            {
                return BadRequest("PermissionId is required.");
            }

            var user = await _db.Set<User>()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                return NotFound($"User {userId} not found.");
            }

            var permission = await _db.Set<Permission>()
                .FirstOrDefaultAsync(p => p.Id == request.PermissionId, ct);

            if (permission is null)
            {
                return NotFound($"Permission {request.PermissionId} not found.");
            }

            var existing = await _db.Set<UserPermission>()
                .FirstOrDefaultAsync(
                    up => up.UserId == userId && up.PermissionId == request.PermissionId,
                    ct);

            if (existing is null)
            {
                existing = new UserPermission
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PermissionId = request.PermissionId,
                    IsGranted = request.IsGranted,
                    IsRevoked = false,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = User?.Identity?.Name ?? "system"
                };

                await _db.Set<UserPermission>().AddAsync(existing, ct);
            }
            else
            {
                existing.IsGranted = request.IsGranted;
                existing.IsRevoked = false;
                existing.AssignedAt = DateTime.UtcNow;
                existing.AssignedBy = User?.Identity?.Name ?? existing.AssignedBy;
            }

            await _db.SaveChangesAsync(ct);

            var dto = new UserPermissionOverrideDto
            {
                PermissionId = existing.PermissionId,
                Code = permission.Code,
                Name = permission.Name,
                IsGranted = existing.IsGranted,
                IsRevoked = existing.IsRevoked,
                AssignedAt = existing.AssignedAt,
                AssignedBy = existing.AssignedBy
            };

            return Ok(dto);
        }

        // ---------------- DELETE: soft-remove override ----------------

        /// <summary>
        /// Soft deletes an override by setting IsRevoked = true.
        /// Effective permission will fall back to plan-level mapping.
        /// </summary>
        [HttpDelete("{permissionId:guid}")]
        public async Task<IActionResult> DeleteOverride(
            Guid userId,
            Guid permissionId,
            CancellationToken ct)
        {
            var overrideRow = await _db.Set<UserPermission>()
                .FirstOrDefaultAsync(
                    up => up.UserId == userId && up.PermissionId == permissionId,
                    ct);

            if (overrideRow is null)
            {
                return NotFound();
            }

            overrideRow.IsRevoked = true;
            // Optional: also reset grant flag – your entitlement logic can ignore revoked rows anyway
            // overrideRow.IsGranted = false;

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
