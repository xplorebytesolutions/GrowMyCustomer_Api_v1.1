// File: Features/AutoReplyBuilder/Models/FlowRunResult.cs

using System;

namespace xbytechat.api.Features.AutoReplyBuilder.Models
{
    /// <summary>
    /// Encapsulates the result of running a visual flow, including agent handoff status.
    /// </summary>
    public class FlowRunResult
    {
        public bool NeedsAgent { get; set; } = false;


        public Guid? HandoffNodeId { get; set; }

        public string? ContextJson { get; set; }

    }
}

