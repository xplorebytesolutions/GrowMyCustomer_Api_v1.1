using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.TeamStaff.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.TeamStaff.Services
{
    /// <summary>
    /// Business-scoped staff management using the existing Users table.
    /// Staff users = Users with BusinessId set + Role assigned.
    /// </summary>
    public sealed class TeamStaffService : ITeamStaffService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TeamStaffService> _logger;

        public TeamStaffService(AppDbContext db, ILogger<TeamStaffService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<StaffUserDto>> GetStaffAsync(Guid businessId)
        {
            var rows = await _db.Users
                .AsNoTracking()
                .Where(u => u.BusinessId == businessId && !u.IsDeleted)
                .Include(u => u.Role)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new StaffUserDto
                {
                    Id = u.Id,
                    BusinessId = u.BusinessId,
                    Name = u.Name,
                    Email = u.Email,
                    RoleId = u.RoleId,
                    RoleName = (u.Role != null && u.Role.Name != null) ? u.Role.Name : "unknown",
                    Status = u.Status ?? "unknown",
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return rows;
        }

        public async Task<ResponseResult> CreateStaffAsync(Guid businessId, Guid actorUserId, CreateStaffUserDto dto)
        {
            // Basic duplicate protection (DB unique index is still recommended)
            var email = (dto.Email ?? "").Trim().ToLowerInvariant();

            var exists = await _db.Users
                .AnyAsync(u => !u.IsDeleted && ((u.Email ?? "").ToLower() == email));

            if (exists)
                return ResponseResult.ErrorInfo("❌ A user with this email already exists.");

            // ✅ Ensure role is assignable INSIDE THIS BUSINESS
            var (ok, role, err) = await TryGetAssignableRoleAsync(businessId, dto.RoleId);
            if (!ok)
                return err!;

            var user = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = dto.Name?.Trim() ?? "",
                Email = email,
                PasswordHash = HashPassword(dto.Password),
                RoleId = role!.Id,
                Status = "Active",          // ✅ Staff should be immediately usable
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Staff user created. StaffUserId={StaffUserId}, BusinessId={BusinessId}, ActorUserId={ActorUserId}",
                user.Id, businessId, actorUserId);

            return ResponseResult.SuccessInfo("✅ Staff user created successfully.", new { user.Id });
        }

        public async Task<ResponseResult> UpdateStaffAsync(Guid businessId, Guid actorUserId, Guid staffUserId, UpdateStaffUserDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == staffUserId && u.BusinessId == businessId && !u.IsDeleted);

            if (user == null)
                return ResponseResult.ErrorInfo("❌ Staff user not found.");

            // Prevent editing yourself via staff screen (avoid self-lockout edge cases)
            if (staffUserId == actorUserId)
                return ResponseResult.ErrorInfo("❌ You cannot edit your own account from TeamStaff.");

            // Prevent changing a business-owner account (role "business") via staff UI
            var currentRoleName = (user.Role?.Name ?? "").Trim().ToLowerInvariant();
            if (currentRoleName == "business")
                return ResponseResult.ErrorInfo("❌ You cannot modify the Business Owner from TeamStaff.");

            // ✅ Ensure new role is assignable INSIDE THIS BUSINESS
            var (ok, newRole, err) = await TryGetAssignableRoleAsync(businessId, dto.RoleId);
            if (!ok)
                return err!;

            user.Name = dto.Name?.Trim() ?? user.Name;
            user.RoleId = newRole!.Id;

            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Staff user updated. StaffUserId={StaffUserId}, BusinessId={BusinessId}, ActorUserId={ActorUserId}",
                staffUserId, businessId, actorUserId);

            return ResponseResult.SuccessInfo("✅ Staff user updated successfully.");
        }

        public async Task<ResponseResult> SetStatusAsync(Guid businessId, Guid actorUserId, Guid staffUserId, string newStatus)
        {
            var normalized = (newStatus ?? "").Trim();
            if (normalized is not ("Active" or "Hold"))
                return ResponseResult.ErrorInfo("❌ Invalid status. Allowed: Active, Hold.");

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == staffUserId && u.BusinessId == businessId && !u.IsDeleted);

            if (user == null)
                return ResponseResult.ErrorInfo("❌ Staff user not found.");

            if (staffUserId == actorUserId)
                return ResponseResult.ErrorInfo("❌ You cannot change your own status.");

            var roleName = (user.Role?.Name ?? "").Trim().ToLowerInvariant();
            if (roleName == "business")
                return ResponseResult.ErrorInfo("❌ You cannot deactivate the Business Owner.");

            user.Status = normalized;
            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Staff status updated. StaffUserId={StaffUserId}, NewStatus={NewStatus}, BusinessId={BusinessId}, ActorUserId={ActorUserId}",
                staffUserId, normalized, businessId, actorUserId);

            return ResponseResult.SuccessInfo($"✅ Staff user status updated to {normalized}.");
        }

        public async Task<List<AssignableRoleDto>> GetAssignableRolesAsync(Guid businessId)
        {
            // only business-scoped roles for that business
            var roles = await _db.Roles
                .AsNoTracking()
                .Where(r => r.IsActive && r.BusinessId == businessId)
                .OrderBy(r => r.Name)
                .Select(r => new AssignableRoleDto
                {
                    Id = r.Id,
                    Name = r.Name
                })
                .ToListAsync();

            return roles;
        }

        /// <summary>
        /// ✅ Central guard: role must belong to this business and be active.
        /// Also blocks admin-type roles from being assigned via TeamStaff.
        /// </summary>
        private async Task<(bool Ok, Role? Role, ResponseResult? Error)> TryGetAssignableRoleAsync(Guid businessId, Guid roleId)
        {
            // ✅ IMPORTANT: role must be scoped to the same business
            var role = await _db.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roleId && r.IsActive && r.BusinessId == businessId);

            if (role == null)
                return (false, null, ResponseResult.ErrorInfo("❌ Invalid role selected."));

            var roleName = (role.Name ?? "").Trim().ToLowerInvariant();
            if (roleName is "admin" or "superadmin" or "partner" or "reseller")
                return (false, null, ResponseResult.ErrorInfo("❌ You cannot assign an admin-type role from TeamStaff."));

            return (true, role, null);
        }

        // 🔒 MVP hashing (same as AuthService)
        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password ?? "");
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
