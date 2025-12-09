using System;

namespace xbytechat.api.Features.AuditTrail.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 📍 Business Context (Multi-Tenant)
        public Guid BusinessId { get; set; }

        // 🙋 Who performed the action
        public Guid PerformedByUserId { get; set; }
        public string? PerformedByUserName { get; set; } // Optional for display
        public string? RoleAtTime { get; set; } // admin / business / agent

        // 🔍 Action Details
        public string ActionType { get; set; } = ""; // e.g., campaign.created, user.login
        public string? Description { get; set; } // Free text for summary or custom note

        // 🌐 Optional: Technical metadata
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Location { get; set; } // Optional for geo-capture later

        // 🕒 Timestamp
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
