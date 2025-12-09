using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Lightweight projection for list screens.
    /// </summary>
    public sealed class AutoReplyFlowSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public string? TriggerKeyword { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Matching mode for this flow:
        /// "Exact" | "Word" | "StartsWith" | "Contains".
        /// Default is "Word".
        /// </summary>
        public string MatchMode { get; set; } = "Word";

        /// <summary>
        /// Priority for choosing between multiple matching flows.
        /// Higher values win. Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
