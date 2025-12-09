using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CRM.Models
{
    public class Tag
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }             // Multi-tenant isolation

        public string Name { get; set; } = default!;     // e.g., "VIP", "Follow-up"

        public string? ColorHex { get; set; }            // For UI tag styling (e.g., #FF5733)

        public string? Category { get; set; }            // e.g., "Priority", "Campaign", "Stage"

        public string? Notes { get; set; }               // Admin/internal notes about this tag

        public bool IsSystemTag { get; set; } = false;   // Reserved tags like "New", "Subscribed"

        public bool IsActive { get; set; } = true;       // For soft-deactivation (future bulk ops)

        public DateTime CreatedAt { get; set; }          // For analytics / sorting

        public DateTime? LastUsedAt { get; set; }        // Useful for CRM insights later

        public ICollection<ContactTag> ContactTags { get; set; } = new List<ContactTag>(); // Linked contacts
    }
}
