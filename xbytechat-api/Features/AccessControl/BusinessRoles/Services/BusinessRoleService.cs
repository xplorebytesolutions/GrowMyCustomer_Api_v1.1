using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AccessControl.BusinessRoles.DTOs;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Models; // Role model lives here in most projects

namespace xbytechat.api.Features.AccessControl.BusinessRoles.Services
{
    public sealed class BusinessRoleService : IBusinessRoleService
    {
        private readonly AppDbContext _db;

        public BusinessRoleService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<BusinessRoleDto>> GetAllAsync(Guid businessId, bool includeInactive = false)
        {
            var q = _db.Roles.AsNoTracking()
                .Where(r => r.BusinessId == businessId);

            if (!includeInactive)
                q = q.Where(r => r.IsActive);

            var rows = await q
                .OrderBy(r => r.Name)
                .Select(r => new BusinessRoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return rows;
        }

        public async Task<BusinessRoleDto?> GetByIdAsync(Guid businessId, Guid roleId)
        {
            return await _db.Roles.AsNoTracking()
                .Where(r => r.Id == roleId && r.BusinessId == businessId)
                .Select(r => new BusinessRoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<BusinessRoleDto> CreateAsync(Guid businessId, BusinessRoleCreateDto dto)
        {
            var name = dto.Name.Trim();

            // Industry-grade: protect system roles (BusinessId == null) from being created/edited here
            var role = new Role
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = name,
                Description = dto.Description?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Roles.Add(role);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Likely your unique index (BusinessId + Name where IsActive=true)
                throw new InvalidOperationException($"Role '{name}' already exists.");
            }

            return new BusinessRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt
            };
        }

        public async Task<BusinessRoleDto> UpdateAsync(Guid businessId, Guid roleId, BusinessRoleUpdateDto dto)
        {
            var role = await _db.Roles
                .Where(r => r.Id == roleId && r.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new KeyNotFoundException("Role not found.");

            // Safety: never allow updating system roles from business endpoints
            if (role.BusinessId == null)
                throw new InvalidOperationException("System roles cannot be edited.");

            role.Name = dto.Name.Trim();
            role.Description = dto.Description?.Trim();

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException($"Role '{role.Name}' already exists.");
            }

            return new BusinessRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt
            };
        }

        public async Task DeactivateAsync(Guid businessId, Guid roleId)
        {
            var role = await _db.Roles
                .Where(r => r.Id == roleId && r.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new KeyNotFoundException("Role not found.");

            if (role.BusinessId == null)
                throw new InvalidOperationException("System roles cannot be deactivated.");

            role.IsActive = false;

            await _db.SaveChangesAsync();
        }
    }
}
