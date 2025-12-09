using System;
using System.Collections.Generic;
using xbytechat.api.AuthModule.Models;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class Role
    {
        public Guid Id { get; set; }

        public string Name { get; set; } // e.g. SuperAdmin, PartnerAdmin, BusinessAdmin, Staff, etc.

        public string? Description { get; set; }

        public bool IsSystemDefined { get; set; } = false; // true for SuperAdmin, PartnerAdmin

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RolePermission> RolePermissions { get; set; }
        public ICollection<User> Users { get; set; } // 🧩 One-to-many relation: Role → Users

    }
}
