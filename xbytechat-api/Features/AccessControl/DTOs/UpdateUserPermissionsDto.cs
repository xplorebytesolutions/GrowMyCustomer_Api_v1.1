using System.Collections.Generic;

namespace xbytechat.api.Features.AccessControl.DTOs.UserPermissions
{
    public sealed class UpdateUserPermissionsDto
    {
        /// <summary>
        /// List of Permission.Code values that should be enabled for this user.
        /// Anything not in this list will be removed.
        /// </summary>
        public List<string> PermissionCodes { get; set; } = new();
    }
}
