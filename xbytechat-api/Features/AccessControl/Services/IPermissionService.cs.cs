// 📄 Features/AccessControl/Services/IPermissionService.cs.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.DTOs;

namespace xbytechat.api.Features.AccessControl.Services
{
    public interface IPermissionService
    {
        // Existing grouped view (used by older UI / internal tools)
        Task<IEnumerable<GroupedPermissionDto>> GetGroupedPermissionsAsync();

        // New flat CRUD endpoints
        Task<IReadOnlyList<PermissionSummaryDto>> GetAllAsync(
            CancellationToken ct = default);

        Task<PermissionSummaryDto> CreateAsync(
            PermissionUpsertDto dto,
            CancellationToken ct = default);

        Task<PermissionSummaryDto> UpdateAsync(
            Guid id,
            PermissionUpsertDto dto,
            CancellationToken ct = default);

        /// <summary>
        /// Soft-delete / deactivate a permission.
        /// </summary>
        Task DeactivateAsync(Guid id, CancellationToken ct = default);
    }
}
