using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CsvBatchUploadResultDto
    {
        public Guid BatchId { get; set; }
        public Guid? AudienceId { get; set; }           // <-- nullable: matches CsvBatch.AudienceId (Guid?)
        public int RowCount { get; set; }
        public List<string> Headers { get; set; } = new();
        public string Message { get; set; } = "CSV batch created.";
        public string FileName { get; set; } = string.Empty;
    }

    public class CsvBatchInfoDto
    {
        public Guid BatchId { get; set; }
        public Guid? AudienceId { get; set; }           // <-- nullable
        public int RowCount { get; set; }
        public List<string> Headers { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class CsvRowSampleDto
    {
        public int RowIndex { get; set; }
        public Dictionary<string, string?> Data { get; set; } = new();
    }
}
