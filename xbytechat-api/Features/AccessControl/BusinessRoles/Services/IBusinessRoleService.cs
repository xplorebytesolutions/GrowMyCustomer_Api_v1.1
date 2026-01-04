using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.BusinessRoles.DTOs;

namespace xbytechat.api.Features.AccessControl.BusinessRoles.Services
{
    public interface IBusinessRoleService
    {
        Task<List<BusinessRoleDto>> GetAllAsync(Guid businessId, bool includeInactive = false);
        Task<BusinessRoleDto?> GetByIdAsync(Guid businessId, Guid roleId);
        Task<BusinessRoleDto> CreateAsync(Guid businessId, BusinessRoleCreateDto dto);
        Task<BusinessRoleDto> UpdateAsync(Guid businessId, Guid roleId, BusinessRoleUpdateDto dto);
        Task DeactivateAsync(Guid businessId, Guid roleId);
    }
}
