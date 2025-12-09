using System;
using xbytechat.api.AuthModule.Models;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class UserPermission
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; }

        public bool IsGranted { get; set; } = true; // ✅ true = allow, false = explicitly deny

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public string? AssignedBy { get; set; } // Admin or system

        public bool IsRevoked { get; set; } = false; // ✅ Required
    }
}
