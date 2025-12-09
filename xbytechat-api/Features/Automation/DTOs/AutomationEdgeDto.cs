using System;

namespace xbytechat.api.Features.Automation.DTOs
{
    /// <summary>
    /// Represents a connection (edge) between two automation nodes.
    /// </summary>
    public class AutomationEdgeDto
    {
        public Guid SourceNodeId { get; set; }

        public Guid TargetNodeId { get; set; }

        public string? Condition { get; set; }  // Optional: for future conditional routing (e.g., "if clicked", "if not responded")
    }
}
