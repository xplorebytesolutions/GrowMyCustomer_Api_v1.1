using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Lightweight read model for showing recent auto-reply triggers
    /// in the builder UI.
    /// </summary>
    public sealed class AutoReplyLogSummaryDto
    {
        public Guid Id { get; set; }
        public string? TriggerType { get; set; }
        public string? TriggerKeyword { get; set; }
        public string? FlowName { get; set; }
        public string? ReplyContent { get; set; }
        public Guid? ContactId { get; set; }
        public Guid? MessageLogId { get; set; }
        public DateTime TriggeredAt { get; set; }
    }
}
