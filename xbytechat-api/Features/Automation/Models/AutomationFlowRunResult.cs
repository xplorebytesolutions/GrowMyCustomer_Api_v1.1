using System;

namespace xbytechat.api.Features.Automation.Models
{
    public class AutomationFlowRunResult
    {
        public bool NeedsAgent { get; set; } = false;

        public Guid? HandoffNodeId { get; set; } = null;

        public string? Notes { get; set; }  // Optional: track execution info (e.g., exit reason)

        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }
}
