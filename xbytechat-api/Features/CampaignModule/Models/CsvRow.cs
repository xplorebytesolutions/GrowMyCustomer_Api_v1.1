using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// One parsed CSV row. Data stored as JSON (key = header, value = cell).
    /// RowIndex is 0-based (first data row = 0) to match streaming ingest.
    /// </summary>
    public class CsvRow
    {
        [Key] public Guid Id { get; set; }

        /// <summary>Tenant scoping for fast filters</summary>
        [Required] public Guid BusinessId { get; set; }

        /// <summary>FK to CsvBatch.Id</summary>
        [Required] public Guid BatchId { get; set; }
        public CsvBatch Batch { get; set; } = null!;

        /// <summary>0-based row number within the batch</summary>
        [Required]
        public int RowIndex { get; set; }

        /// <summary>Raw phone, exactly as uploaded (optional convenience)</summary>
        [MaxLength(64)]
        public string? PhoneRaw { get; set; }

        /// <summary>Normalized phone in E.164 (+&lt;country&gt;&lt;number&gt;)</summary>
        [MaxLength(32)]
        public string? PhoneE164 { get; set; }

        /// <summary>JSON of the row: {"header":"value", ...}</summary>
        public string RowJson { get; set; } = "{}";

        /// <summary>
        /// Back-compat shim for code that uses DataJson.
        /// Not mapped to its own column; simply forwards to RowJson.
        /// </summary>
        [NotMapped]
        public string? DataJson
        {
            get => RowJson;
            set => RowJson = value ?? "{}";
        }

        /// <summary>If invalid at ingest/validation time, store reason here</summary>
        public string? ValidationError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
