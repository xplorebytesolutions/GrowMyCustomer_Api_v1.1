using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Tracking.Services
{
    public interface IJourneyExportService
    {
        Task<ExportResult> ExportJourneyCsvAsync(Guid campaignSendLogId, CancellationToken ct = default);
        Task<ExportResult> ExportJourneyXlsxAsync(Guid campaignSendLogId, CancellationToken ct = default);
    }

    public sealed class ExportResult
    {
        public required byte[] Bytes { get; init; }
        public required string ContentType { get; init; }
        public required string FileName { get; init; }
    }
}
