using System;

namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    /// <summary>
    /// Describes where a flow execution was started from.
    /// This is the key for analytics segmentation.
    /// </summary>
    public enum FlowExecutionOrigin
    {
        /// <summary>
        /// Default / legacy rows before origin tracking was introduced.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Flow started as part of a Campaign CTA (button click, deep link, etc.).
        /// </summary>
        Campaign = 1,

        /// <summary>
        /// Flow started from AutoReply word matching (keyword → flow).
        /// </summary>
        AutoReply = 2,

        /// <summary>
        /// Flow started from a future “JourneyBot” or similar orchestration engine.
        /// </summary>
        JourneyBot = 3,

        /// <summary>
        /// Flow started manually from Inbox or agent tools.
        /// </summary>
        Inbox = 4,

        /// <summary>
        /// System-driven or other internal triggers (backfill, test, etc.).
        /// </summary>
        System = 5
    }
}
