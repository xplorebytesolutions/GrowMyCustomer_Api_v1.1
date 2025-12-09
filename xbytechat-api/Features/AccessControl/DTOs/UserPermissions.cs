namespace xbytechat.api.Features.AccessControl.DTOs.UserPermissions
{
    public sealed class UserPermissionItemDto
    {
        public string PermissionCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsAssigned { get; set; }
    }
}
