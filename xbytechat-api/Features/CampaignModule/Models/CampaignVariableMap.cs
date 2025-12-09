using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// Maps a template placeholder to a data source for a specific campaign.
    /// Examples of Component:
    ///   "body", "header", "button:url:1"
    /// Index is 1-based ({{1}}, {{2}}, ...).
    /// </summary>
    public class CampaignVariableMap
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        /// <summary> "body" | "header" | "button:url:1" </summary>
        [Required, MaxLength(64)]
        public string Component { get; set; } = null!;

        /// <summary> 1..N (corresponds to {{index}}) </summary>
        [Required]
        public int Index { get; set; }

        /// <summary>
        /// ContactField | CsvColumn | Static | Expression
        /// </summary>
        [Required, MaxLength(32)]
        public string SourceType { get; set; } = null!;

        /// <summary>
        /// If SourceType == ContactField → "name","phone","email",...
        /// If SourceType == CsvColumn → CSV header name.
        /// Otherwise null.
        /// </summary>
        [MaxLength(128)]
        public string? SourceKey { get; set; }

        /// <summary>Used when SourceType == Static</summary>
        public string? StaticValue { get; set; }

        /// <summary>Optional expression (mini DSL) for computed values</summary>
        public string? Expression { get; set; }

        /// <summary>Fallback when source is empty/invalid</summary>
        public string? DefaultValue { get; set; }

        /// <summary>If true, missing value = validation error in dry-run</summary>
        public bool IsRequired { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedByUserId { get; set; }
        public Guid BusinessId { get; set; }  // denormalized for ownership checks
    }
}
