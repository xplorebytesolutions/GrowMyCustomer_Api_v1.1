using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignCsvMaterializeRequestDto
    {
        [Required] public Guid CsvBatchId { get; set; }
        public Dictionary<string, string>? Mappings { get; set; } // token -> header or "constant:Value"
        public string? PhoneField { get; set; }
        public bool NormalizePhones { get; set; } = true;
        public bool Deduplicate { get; set; } = true;
        public int? Limit { get; set; } = 200;

        public bool Persist { get; set; } = false;
        public string? AudienceName { get; set; } // required when Persist=true
    }

    public sealed class CsvMaterializedRowDto
    {
        public int RowIndex { get; set; }
        public string? Phone { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public sealed class CampaignCsvMaterializeResponseDto
    {
        public Guid CampaignId { get; set; }
        public Guid CsvBatchId { get; set; }
        public int TotalRows { get; set; }
        public int MaterializedCount { get; set; }
        public int SkippedCount { get; set; }
        public Guid? AudienceId { get; set; }          // if persisted
        public List<CsvMaterializedRowDto> Preview { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
