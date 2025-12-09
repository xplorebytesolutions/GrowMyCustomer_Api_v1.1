using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// Represents a single CSV upload (file) for a business.
    /// Stores headers and basic metadata; rows live in CsvRow.
    /// </summary>
    public class CsvBatch
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid BusinessId { get; set; }

        public Guid? AudienceId { get; set; }
        /// <summary>Original filename, if available</summary>
        [MaxLength(256)]
        public string? FileName { get; set; }

      
        /// <summary>Comma-separated or JSON array of headers (we’ll map to jsonb via DbContext)</summary>
        public string? HeadersJson { get; set; }

        /// <summary>SHA256 (or similar) of file contents for dedupe</summary>
        [MaxLength(128)]
        public string? Checksum { get; set; }

        /// <summary>Total rows parsed (including headerless lines after validation)</summary>
        public int RowCount { get; set; }

        /// <summary>Total rows skipped due to validation</summary>
        public int SkippedCount { get; set; }

        [MaxLength(32)]
        public string Status { get; set; } = "ready"; // ready | ingesting | failed | complete

        public string? ErrorMessage { get; set; }

        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
