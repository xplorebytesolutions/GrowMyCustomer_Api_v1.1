using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.Automation.Models
{
    /// <summary>
    /// Represents a saved automation flow with nodes and edges.
    /// </summary>
    public class AutomationFlow
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string TriggerKeyword { get; set; } = string.Empty; // ✅ Better naming

        public string NodesJson { get; set; } = "[]";

        public string EdgesJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
