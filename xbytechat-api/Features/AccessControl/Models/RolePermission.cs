using System;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class RolePermission
    {
        public Guid Id { get; set; }

        public Guid RoleId { get; set; }
        public Role Role { get; set; }

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public string? AssignedBy { get; set; } // Admin user email or ID

        public bool IsActive { get; set; } = true; // ✅ Add this line
        public bool IsRevoked { get; set; } = false; // ✅ Required
    }
}
