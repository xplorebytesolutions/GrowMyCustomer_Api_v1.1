using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.AutoReplyBuilder.Models
{
    [Table("AutoReplyLogs")]
    public class AutoReplyLog
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }

        public string TriggerKeyword { get; set; } = string.Empty; // e.g., "hi", "price"
        public string TriggerType { get; set; } = "flow";    // "flow" or "rule"

        public string ReplyContent { get; set; } = string.Empty;    // Plaintext summary of what was sent
        public string? FlowName { get; set; }                       // Nullable if rule-based

        public Guid? MessageLogId { get; set; }                     // Optional link to MessageLog
        public DateTime TriggeredAt { get; set; }
    }
}
