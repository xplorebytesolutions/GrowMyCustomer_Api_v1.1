using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// DTO used to log when an AutoReply flow is triggered for a contact.
    /// </summary>
    public class AutoReplyLogDto
    {
        /// <summary>
        /// Primary key of the log entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Business (tenant) that owns this log entry.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Contact that triggered this auto-reply.
        /// </summary>
        public Guid ContactId { get; set; }

        /// <summary>
        /// Type of trigger. Currently always "flow" since the legacy rule engine is removed.
        /// </summary>
        public string TriggerType { get; set; } = "flow";

        /// <summary>
        /// The keyword that matched and caused the flow to trigger.
        /// </summary>
        public string TriggerKeyword { get; set; } = string.Empty;

        /// <summary>
        /// The reply content that was sent (for auditing / analytics).
        /// </summary>
        public string ReplyContent { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when the auto-reply was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; }

        /// <summary>
        /// Optional human-friendly name of the flow that handled the message.
        /// </summary>
        public string? FlowName { get; set; }

        /// <summary>
        /// Optional reference to the outbound MessageLog row that represents the sent auto-reply.
        /// </summary>
        public Guid? MessageLogId { get; set; }
    }
}
