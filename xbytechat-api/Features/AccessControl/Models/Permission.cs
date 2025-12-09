using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class Permission
    {
        public Guid Id { get; set; }

        public string Code { get; set; } // Unique key like "ViewDashboard"

        public string Name { get; set; } // Friendly name like "View Dashboard"

        public string? Group { get; set; } // Optional grouping, e.g., "CRM", "Catalog", "Admin"

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RolePermission> RolePermissions { get; set; }

        public ICollection<UserPermission> UserPermissions { get; set; }

        public ICollection<PlanPermission> PlanPermissions { get; set; }
    }
}
