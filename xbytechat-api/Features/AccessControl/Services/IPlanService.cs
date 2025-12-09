using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.DTOs;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Services
{
    public interface IPlanService
    {
        Task<IEnumerable<PlanDto>> GetAllPlansAsync();
        Task<IEnumerable<PermissionDto>> GetPermissionsForPlanAsync(Guid planId);
       // Task UpdatePlanPermissionsAsync(Guid planId, List<Guid> permissionIds);
        Task<Guid> CreatePlanAsync(CreatePlanDto dto);
        Task<bool> DeletePlanAsync(Guid planId);
        Task<bool> UpdatePlanAsync(Guid planId, UpdatePlanDto dto);

        // New methods for permissions
        Task<List<PermissionDto>> GetPlanPermissionsAsync(Guid planId);
        //Task UpdatePlanPermissionsAsync(Guid planId, List<Guid> permissionIds);
        Task UpdatePlanPermissionsAsync(
    Guid planId,
    IReadOnlyCollection<Guid> permissionIds,
    bool replaceAll,
    string? assignedBy,
    CancellationToken ct);
        Task<PlanDto?> GetByIdAsync(Guid planId, CancellationToken ct = default);
    }
}

