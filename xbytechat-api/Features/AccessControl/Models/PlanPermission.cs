using System;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class PlanPermission
    {
        public Guid Id { get; set; }

        public Guid PlanId { get; set; }
        public Plan Plan { get; set; }

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public string? AssignedBy { get; set; } // Admin email or ID
    }
}

