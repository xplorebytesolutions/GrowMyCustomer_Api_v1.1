using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Simple DTO used to toggle the active status of an AutoReplyFlow
    /// from the Flows modal (Activate / Deactivate).
    /// </summary>
    public sealed class AutoReplyFlowStatusUpdateDto
    {
        /// <summary>
        /// True to mark the flow as active, false to deactivate it.
        /// </summary>
        public bool IsActive { get; set; }
    }
}
