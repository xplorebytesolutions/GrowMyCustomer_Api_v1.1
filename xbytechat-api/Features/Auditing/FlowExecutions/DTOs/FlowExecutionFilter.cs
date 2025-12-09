using System;
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.Auditing.FlowExecutions.DTOs
{
    /// <summary>
    /// Filter options when querying flow execution logs.
    /// All fields are optional except BusinessId (which is passed separately).
    /// </summary>
    public class FlowExecutionFilter
    {
        /// <summary>
        /// Optional: restrict to a specific origin (AutoReply, Campaign, etc.).
        /// </summary>
        public FlowExecutionOrigin? Origin { get; set; }

        /// <summary>
        /// Optional: restrict to a specific flow id.
        /// This is usually the CTAFlowConfig.Id or AutoReplyFlow.Id,
        /// depending on how FlowId is populated.
        /// </summary>
        public Guid? FlowId { get; set; }

        /// <summary>
        /// Optional: restrict to a specific contact phone number.
        /// Stored exactly as in FlowExecutionLogs (usually WhatsApp "from" number).
        /// </summary>
        public string? ContactPhone { get; set; }

        /// <summary>
        /// Maximum number of rows to return.
        /// Defaults to 50; upper capped in service for safety.
        /// </summary>
        public int Limit { get; set; } = 50;
    }
}

