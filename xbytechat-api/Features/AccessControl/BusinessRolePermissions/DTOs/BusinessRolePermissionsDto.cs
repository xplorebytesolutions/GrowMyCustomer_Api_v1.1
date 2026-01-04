using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AccessControl.BusinessRolePermissions.DTOs
{
    public sealed class BusinessRolePermissionsDto
    {
        public Guid RoleId { get; set; }
        public List<string> PermissionCodes { get; set; } = new();
    }
}
