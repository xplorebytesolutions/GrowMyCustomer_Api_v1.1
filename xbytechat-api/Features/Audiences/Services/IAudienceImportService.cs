using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Audiences.DTOs;

namespace xbytechat.api.Features.Audiences.Services
{
    public interface IAudienceImportService
    {
        /// <summary>
        /// Parses a CSV stream (first row = headers), creates a CsvBatch and CsvRows, and returns batch summary.
        /// </summary>
        Task<CsvImportResponseDto> ImportCsvAsync(
            Guid businessId,
            Stream csvStream,
            string fileName,
            CancellationToken ct = default);
    }
}
