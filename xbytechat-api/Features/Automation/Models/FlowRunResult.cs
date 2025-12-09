using System;

namespace xbytechat.api.Features.Automation.Models
{
    /// <summary>
    /// Represents the result of running an automation flow.
    /// </summary>
    public class FlowRunResult
    {
        public bool NeedsAgent { get; set; } = false;

        public Guid? HandoffNodeId { get; set; } // If agent handoff requested
    }
}
