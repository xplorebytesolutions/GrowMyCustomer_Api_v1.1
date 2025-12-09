using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    /// <summary>
    /// Lightweight projection for listing recent CSV batches.
    /// </summary>
    public sealed class CsvBatchListItemDto
    {
        public Guid BatchId { get; set; }
        public string? FileName { get; set; }
        public int RowCount { get; set; }
        public string Status { get; set; } = "ready"; // ready | ingesting | failed | complete
        public DateTime CreatedAt { get; set; }
    }
}
