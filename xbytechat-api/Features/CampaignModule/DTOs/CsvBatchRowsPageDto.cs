using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    /// <summary>
    /// Paged slice of CSV rows for previewing a batch.
    /// </summary>
    public sealed class CsvBatchRowsPageDto
    {
        public Guid BatchId { get; set; }
        public int TotalRows { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<CsvRowSampleDto> Rows { get; set; } = new();
    }
}
