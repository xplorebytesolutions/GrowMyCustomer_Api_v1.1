namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class UpdatePlanPermissionsRequest
    {
        public List<Guid> PermissionIds { get; set; } = new();
        public bool ReplaceAll { get; set; } = true; // optional flag your controller reads
    }
}
