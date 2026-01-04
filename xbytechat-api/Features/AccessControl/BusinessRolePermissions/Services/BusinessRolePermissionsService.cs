using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AccessControl.BusinessRolePermissions.DTOs;

namespace xbytechat.api.Features.AccessControl.BusinessRolePermissions.Services
{
    public sealed class BusinessRolePermissionsService : IBusinessRolePermissionsService
    {
        private readonly AppDbContext _db;

        public BusinessRolePermissionsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<BusinessRolePermissionsDto> GetAsync(Guid businessId, Guid roleId)
        {
            // Ensure role belongs to this business and is not a system role
            var role = await _db.Roles.AsNoTracking()
                .Where(r => r.Id == roleId && r.BusinessId == businessId)
                .Select(r => new { r.Id, r.BusinessId })
                .FirstOrDefaultAsync();

            if (role == null)
                throw new KeyNotFoundException("Role not found.");

            if (role.BusinessId == null)
                throw new InvalidOperationException("System role permissions cannot be managed here.");

            var codes = await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == roleId)
                .Join(_db.Permissions.AsNoTracking(),
                      rp => rp.PermissionId,
                      p => p.Id,
                      (rp, p) => p.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return new BusinessRolePermissionsDto
            {
                RoleId = roleId,
                PermissionCodes = codes
            };
        }

        public async Task<BusinessRolePermissionsDto> ReplaceAsync(Guid businessId, Guid roleId, UpdateBusinessRolePermissionsDto dto)
        {
            var role = await _db.Roles
                .Where(r => r.Id == roleId && r.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new KeyNotFoundException("Role not found.");

            if (role.BusinessId == null)
                throw new InvalidOperationException("System role permissions cannot be managed here.");

            // Normalize codes (trim + distinct + uppercase optional)
            var requestedCodes = (dto.PermissionCodes ?? new List<string>())
                .Select(x => (x ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Validate permission codes exist
            var perms = await _db.Permissions.AsNoTracking()
                .Where(p => requestedCodes.Contains(p.Code))
                .Select(p => new { p.Id, p.Code })
                .ToListAsync();

            var foundCodes = perms.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Unknown permission codes: {string.Join(", ", missing)}");

            // Replace mappings in one transaction
            await using var tx = await _db.Database.BeginTransactionAsync();

            var old = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            if (old.Count > 0)
                _db.RolePermissions.RemoveRange(old);

            if (perms.Count > 0)
            {
                var newRows = perms.Select(p => new Models.RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    PermissionId = p.Id,
                    AssignedAt = DateTime.UtcNow
                }).ToList();

                await _db.RolePermissions.AddRangeAsync(newRows);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new BusinessRolePermissionsDto
            {
                RoleId = roleId,
                PermissionCodes = perms.Select(p => p.Code).OrderBy(x => x).ToList()
            };
        }
    }
}
