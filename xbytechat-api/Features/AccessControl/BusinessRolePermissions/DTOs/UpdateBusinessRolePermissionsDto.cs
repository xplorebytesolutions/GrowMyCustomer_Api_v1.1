using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.AccessControl.BusinessRolePermissions.DTOs
{
    public sealed class UpdateBusinessRolePermissionsDto
    {
        [Required]
        public List<string> PermissionCodes { get; set; } = new();
    }
}
