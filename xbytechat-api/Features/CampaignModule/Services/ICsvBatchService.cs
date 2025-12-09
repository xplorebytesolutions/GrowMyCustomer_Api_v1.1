using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICsvBatchService
    {
        //Task<CsvBatchUploadResultDto> CreateAndIngestAsync(
        //    Guid businessId,
        //    string fileName,
        //    Stream stream,
        //    Guid? audienceId = null,
        //    CancellationToken ct = default
        //);
    
            Task<CsvBatchUploadResultDto> CreateAndIngestAsync(
                Guid businessId,
                string fileName,
                Stream stream,
                Guid? audienceId = null,
                Guid? campaignId = null,
                CancellationToken ct = default);
      

        Task<CsvBatchInfoDto?> GetBatchAsync(Guid businessId, Guid batchId, CancellationToken ct = default);
        Task<IReadOnlyList<CsvRowSampleDto>> GetSamplesAsync(Guid businessId, Guid batchId, int take = 20, CancellationToken ct = default);
        Task<CsvBatchValidationResultDto> ValidateAsync(Guid businessId, Guid batchId, CsvBatchValidationRequestDto request, CancellationToken ct = default);
        Task<List<CsvBatchListItemDto>> ListBatchesAsync(Guid businessId, int limit = 20, CancellationToken ct = default);
        Task<CsvBatchRowsPageDto> GetRowsPageAsync(Guid businessId, Guid batchId, int skip, int take, CancellationToken ct = default);
        Task<bool> DeleteBatchAsync(Guid businessId, Guid batchId, CancellationToken ct = default);
    }
}
