using System;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.BusinessRolePermissions.DTOs;

namespace xbytechat.api.Features.AccessControl.BusinessRolePermissions.Services
{
    public interface IBusinessRolePermissionsService
    {
        Task<BusinessRolePermissionsDto> GetAsync(Guid businessId, Guid roleId);
        Task<BusinessRolePermissionsDto> ReplaceAsync(Guid businessId, Guid roleId, UpdateBusinessRolePermissionsDto dto);
    }
}
