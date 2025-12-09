using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class UpdateRolePermissionsDto
    {
        [Required]
        public List<Guid> PermissionIds { get; set; } = new();
        public bool ReplaceAll { get; set; } = true;
    }
}
