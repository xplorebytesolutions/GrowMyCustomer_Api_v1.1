using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.TeamStaff.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.TeamStaff.Services
{
    public interface ITeamStaffService
    {
        Task<List<StaffUserDto>> GetStaffAsync(Guid businessId);

        Task<ResponseResult> CreateStaffAsync(Guid businessId, Guid actorUserId, CreateStaffUserDto dto);

        Task<ResponseResult> UpdateStaffAsync(Guid businessId, Guid actorUserId, Guid staffUserId, UpdateStaffUserDto dto);

        Task<ResponseResult> SetStatusAsync(Guid businessId, Guid actorUserId, Guid staffUserId, string newStatus);
        Task<List<AssignableRoleDto>> GetAssignableRolesAsync(Guid businessId);

    }
}
