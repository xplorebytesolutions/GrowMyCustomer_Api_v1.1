using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignMaterializeRequestDto
    {
        [Required] public Guid CsvBatchId { get; set; }

        /// <summary>
        /// Optional explicit mapping: token -> CSV header name or "constant:Value".
        /// If null/empty, we’ll try loading saved mappings; if none exist, we fall back to header==token.
        /// </summary>
        public Dictionary<string, string>? Mappings { get; set; }

        /// <summary>
        /// If not provided, we will try common headers like phone, mobile, whatsapp, msisdn.
        /// </summary>
        public string? PhoneField { get; set; }

        public bool NormalizePhones { get; set; } = true;
        public bool Deduplicate { get; set; } = true;

        /// <summary>Preview only first N rows; 0 or null means all.</summary>
        public int? Limit { get; set; } = 200;

        /// <summary>When true, materialized rows are persisted to Audience + CampaignRecipients.</summary>
        public bool Persist { get; set; } = false;

        /// <summary>Required when Persist = true.</summary>
        public string? AudienceName { get; set; }
    }

    public sealed class MaterializedRowDto
    {
        public int RowIndex { get; set; }
        public string? Phone { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public sealed class CampaignMaterializeResponseDto
    {
        public Guid CampaignId { get; set; }
        public Guid CsvBatchId { get; set; }
        public int TotalRows { get; set; }
        public int MaterializedCount { get; set; }
        public int SkippedCount { get; set; }
        public Guid? AudienceId { get; set; } // when persisted
        public List<MaterializedRowDto> Preview { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
