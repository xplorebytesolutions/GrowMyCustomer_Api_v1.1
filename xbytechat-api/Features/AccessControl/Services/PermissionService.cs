// 📄 Features/AccessControl/Services/PermissionService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AccessControl.DTOs;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly AppDbContext _context;

        public PermissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<GroupedPermissionDto>> GetGroupedPermissionsAsync()
        {
            // Legacy / grouped view (kept for compatibility)
            return await _context.Permissions
                .Where(p => p.IsActive)
                .GroupBy(p => p.Group ?? "Ungrouped")
                .Select(g => new GroupedPermissionDto
                {
                    Group = g.Key,
                    Features = g.ToList()
                })
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PermissionSummaryDto>> GetAllAsync(
            CancellationToken ct = default)
        {
            return await _context.Permissions
                .OrderBy(p => p.Group)
                .ThenBy(p => p.Code)
                .Select(p => new PermissionSummaryDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Group = p.Group,
                    Description = p.Description,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task<PermissionSummaryDto> CreateAsync(
            PermissionUpsertDto dto,
            CancellationToken ct = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var code = dto.Code?.Trim();
            var name = dto.Name?.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Code and Name are required.");
            }

            var normalizedCode = code.ToUpperInvariant();

            var exists = await _context.Permissions
                .AnyAsync(p => p.Code == normalizedCode, ct);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Permission code '{normalizedCode}' already exists.");
            }

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Code = normalizedCode,
                Name = name,
                Group = string.IsNullOrWhiteSpace(dto.Group)
                    ? null
                    : dto.Group!.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description!.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync(ct);

            return ToSummary(permission);
        }

        public async Task<PermissionSummaryDto> UpdateAsync(
            Guid id,
            PermissionUpsertDto dto,
            CancellationToken ct = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (permission == null)
                throw new KeyNotFoundException("Permission not found.");

            var name = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Name is required.");

            // Code is intentionally immutable (UI keeps it disabled).
            permission.Name = name;
            permission.Group = string.IsNullOrWhiteSpace(dto.Group)
                ? null
                : dto.Group!.Trim();
            permission.Description = string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description!.Trim();

            await _context.SaveChangesAsync(ct);

            return ToSummary(permission);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (permission == null)
                throw new KeyNotFoundException("Permission not found.");

            if (!permission.IsActive)
                return;

            permission.IsActive = false;
            await _context.SaveChangesAsync(ct);
        }

        private static PermissionSummaryDto ToSummary(Permission p)
        {
            return new PermissionSummaryDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Group = p.Group,
                Description = p.Description,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            };
        }
    }
}
