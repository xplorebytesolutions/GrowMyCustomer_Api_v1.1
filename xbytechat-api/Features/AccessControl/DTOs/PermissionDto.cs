namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string? Group { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
