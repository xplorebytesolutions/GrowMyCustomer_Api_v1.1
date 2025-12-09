using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.DTOs;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Services
{
    public class PlanService : IPlanService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlanService> _logger;
        private readonly IPermissionCacheService _permissionCacheService;

        public PlanService(AppDbContext context, ILogger<PlanService> logger, IPermissionCacheService permissionCacheService)
        {
            _context = context;
            _logger = logger;
            _permissionCacheService = permissionCacheService;
        }



        public async Task<IEnumerable<PlanDto>> GetAllPlansAsync()
        {
            return await _context.Plans
                .Where(p => p.IsActive)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    IsActive = p.IsActive
                })
                .ToListAsync();
        }
        public async Task<IEnumerable<PermissionDto>> GetPermissionsForPlanAsync(Guid planId)
        {
            return await _context.PlanPermissions
                .Where(pp => pp.PlanId == planId && pp.IsActive)
                .Select(pp => new PermissionDto
                {
                    Id = pp.Permission.Id,
                    Code = pp.Permission.Code,
                    Name = pp.Permission.Name,
                    Group = pp.Permission.Group,
                    Description = pp.Permission.Description,
                    IsActive = pp.Permission.IsActive
                })
                .ToListAsync();
        }
        public async Task<PlanDto?> GetByIdAsync(Guid planId, CancellationToken ct = default)
        {
            return await _context.Plans
                .AsNoTracking()
                .Where(p => p.Id == planId)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    IsActive = p.IsActive
                })
                .FirstOrDefaultAsync(ct);
        }

        //public async Task<IEnumerable<Permission>> GetPermissionsForPlanAsync(Guid planId)
        //{
        //    _logger.LogInformation("Fetching permissions for plan {PlanId}", planId);
        //    try
        //    {
        //        return await _context.PlanPermissions
        //            .Where(pp => pp.PlanId == planId && pp.IsActive)
        //            .Include(pp => pp.Permission)
        //            .Select(pp => pp.Permission)
        //            .AsNoTracking()
        //            .ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching permissions for plan {PlanId}", planId);
        //        throw;
        //    }
        //}

        //public async Task UpdatePlanPermissionsAsync(Guid planId, List<Guid> permissionIds)
        //{
        //    _logger.LogInformation("Updating permissions for plan {PlanId}", planId);
        //    try
        //    {
        //        // Remove all existing permissions for the plan
        //        var existing = await _context.PlanPermissions
        //            .Where(pp => pp.PlanId == planId)
        //            .ToListAsync();

        //        _context.PlanPermissions.RemoveRange(existing);

        //        // Add new permissions
        //        var newPlanPermissions = permissionIds.Select(pid => new PlanPermission
        //        {
        //            Id = Guid.NewGuid(),
        //            PlanId = planId,
        //            PermissionId = pid,
        //            IsActive = true,
        //            AssignedAt = DateTime.UtcNow,
        //            AssignedBy = "System"
        //        });

        //        await _context.PlanPermissions.AddRangeAsync(newPlanPermissions);
        //        await _context.SaveChangesAsync();
        //        _logger.LogInformation("Permissions updated for plan {PlanId}", planId);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error updating permissions for plan {PlanId}", planId);
        //        throw;
        //    }
        //}

        public async Task<Guid> CreatePlanAsync(CreatePlanDto dto)
        {
            _logger.LogInformation("Creating new plan: {PlanName}", dto.Name);
            try
            {
                var plan = new Plan
                {
                    Id = Guid.NewGuid(),
                    Code = dto.Code,
                    Name = dto.Name,
                    Description = dto.Description,
                    IsActive = dto.IsActive
                };

                _context.Plans.Add(plan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Plan created with ID: {PlanId}", plan.Id);
                return plan.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plan {PlanName}", dto.Name);
                throw;
            }
        }

        public async Task<bool> DeletePlanAsync(Guid planId)
        {
            _logger.LogInformation("Deleting (soft) plan {PlanId}", planId);
            try
            {
                var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId);
                if (plan == null || !plan.IsActive)
                {
                    _logger.LogWarning("Plan not found or already inactive: {PlanId}", planId);
                    return false;
                }

                plan.IsActive = false;
                _context.Plans.Update(plan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Plan {PlanId} soft deleted.", planId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting plan {PlanId}", planId);
                throw;
            }
        }

        public async Task<bool> UpdatePlanAsync(Guid planId, UpdatePlanDto dto)
        {
            _logger.LogInformation("Updating plan {PlanId}", planId);
            try
            {
                var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId);
                if (plan == null)
                {
                    _logger.LogWarning("Plan not found: {PlanId}", planId);
                    return false;
                }

                plan.Code = dto.Code;
                plan.Name = dto.Name;
                plan.Description = dto.Description;
                plan.IsActive = dto.IsActive;

                _context.Plans.Update(plan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Plan {PlanId} updated successfully.", planId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating plan {PlanId}", planId);
                throw;
            }
        }

        public async Task UpdatePlanPermissionsAsync(
         Guid planId,
         IReadOnlyCollection<Guid> permissionIds,
         bool replaceAll,
         string? assignedBy,
         CancellationToken ct)
        {
            using var trx = await _context.Database.BeginTransactionAsync(ct);

            var existing = await _context.PlanPermissions
                .Where(pp => pp.PlanId == planId)
                .ToListAsync(ct);

            var want = new HashSet<Guid>(permissionIds);
            var byPerm = existing.ToDictionary(pp => pp.PermissionId);

            // upsert requested permissions → Active
            foreach (var pid in want)
            {
                if (byPerm.TryGetValue(pid, out var row))
                {
                    if (!row.IsActive)
                    {
                        row.IsActive = true;
                        row.AssignedAt = DateTime.UtcNow;
                        row.AssignedBy = assignedBy;
                        _context.PlanPermissions.Update(row);
                    }
                }
                else
                {
                    _context.PlanPermissions.Add(new PlanPermission
                    {
                        Id = Guid.NewGuid(),
                        PlanId = planId,
                        PermissionId = pid,
                        IsActive = true,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = assignedBy
                    });
                }
            }

            // replaceAll => deactivate anything not requested
            if (replaceAll)
            {
                foreach (var row in existing)
                {
                    if (!want.Contains(row.PermissionId) && row.IsActive)
                    {
                        row.IsActive = false;
                        _context.PlanPermissions.Update(row);
                    }
                }
            }

            await _context.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);

            // Bust the plan permission cache
            _permissionCacheService.ClearPlanPermissionsCache(planId);
        }



        //public async Task UpdatePlanPermissionsAsync(Guid planId, List<Guid> permissionIds)
        //{
        //    // Remove old mappings
        //    var existing = await _context.PlanPermissions
        //        .Where(pp => pp.PlanId == planId)
        //        .ToListAsync();
        //    _context.PlanPermissions.RemoveRange(existing);

        //    // Add new mappings
        //    var newMappings = permissionIds.Select(pid => new PlanPermission
        //    {
        //        PlanId = planId,
        //        PermissionId = pid,
        //        AssignedAt = DateTime.UtcNow,
        //        AssignedBy = "system" // replace with logged-in admin
        //    });

        //    await _context.PlanPermissions.AddRangeAsync(newMappings);
        //    await _context.SaveChangesAsync();
        //    //// Clear cache
        //    _permissionCacheService.ClearPlanPermissionsCache(planId);
        //}
        public async Task<List<PermissionDto>> GetPlanPermissionsAsync(Guid planId)
        {
            return await _context.PlanPermissions
                .Where(pp => pp.PlanId == planId && pp.IsActive)
                .Select(pp => new PermissionDto
                {
                    Id = pp.Permission.Id,
                    Code = pp.Permission.Code,
                    Name = pp.Permission.Name,
                    Group = pp.Permission.Group,
                    Description = pp.Permission.Description
                })
                .ToListAsync();
        }
    }
}
