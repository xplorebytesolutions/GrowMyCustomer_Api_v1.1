using System;
using System.Collections.Generic;
using xbytechat.api.Features.BusinessModule.Models;

namespace xbytechat.api.Features.AccessControl.Models
{
    public class Plan
    {
        public Guid Id { get; set; }

        public string Code { get; set; } // e.g. "FREE", "SMART", "ADVANCED"
        public string Name { get; set; } // Friendly display name

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsInternal { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PlanPermission> PlanPermissions { get; set; }
        public ICollection<Business> Businesses { get; set; } = new List<Business>();
    }
}
