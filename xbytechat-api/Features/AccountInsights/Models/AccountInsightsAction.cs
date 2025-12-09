using System;

namespace xbytechat.api.Features.AccountInsights.Models
{
    /// <summary>
    /// Lightweight append-only log of important micro-actions taken on an account.
    /// Used to power the Recent Activity timeline in Account Insights UI.
    /// </summary>
    public class AccountInsightsAction
    {
        public long Id { get; set; }

        public Guid BusinessId { get; set; }

        /// <summary>
        /// Machine-friendly action code, e.g. "TAG_CONTACTED", "EXTEND_TRIAL".
        /// </summary>
        public string ActionType { get; set; }

        /// <summary>
        /// Human-readable label shown in the UI timeline.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Who performed the action (user email/id) or "system".
        /// </summary>
        public string Actor { get; set; }

        /// <summary>
        /// Optional JSON blob for extra context (e.g. { "extraDays": 7 }).
        /// Stored as string for simplicity; can be mapped to JSONB on the DB side.
        /// </summary>
        public string MetaJson { get; set; }

        /// <summary>
        /// UTC timestamp when the action occurred.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
