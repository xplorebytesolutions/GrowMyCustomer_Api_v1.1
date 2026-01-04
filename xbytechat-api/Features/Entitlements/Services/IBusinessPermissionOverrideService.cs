using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Entitlements.Services
{
    public interface IBusinessPermissionOverrideService
    {
        Task<List<BusinessPermissionOverrideDto>> GetAsync(Guid businessId);
        Task<ResponseResult> UpsertAsync(Guid businessId, Guid actorUserId, UpsertBusinessPermissionOverrideDto dto);
        Task<ResponseResult> RevokeByPermissionCodeAsync(Guid businessId, Guid actorUserId, string permissionCode);
    }
}
