using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class GroupedPermissionDto
    {
        public string Group { get; set; }
        public List<Permission> Features { get; set; }
    }
}
