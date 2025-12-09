using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.Audiences.DTOs
{
    public class CsvImportResponseDto
    {
        public Guid BatchId { get; set; }
        public int RowCount { get; set; }
        public List<string> Columns { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
