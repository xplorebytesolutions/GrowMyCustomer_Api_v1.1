// 📄 Features/Tracking/Services/IMessageLogsReportService.cs
using xbytechat.api.Shared; // PaginatedResponse<T>
using xbytechat.api.Features.Tracking.DTOs;

namespace xbytechat.api.Features.Tracking.Services
{
    public interface IMessageLogsReportService
    {

        Task<PaginatedResponse<MessageLogListItemDto>> SearchAsync(
            Guid businessId,
            MessageLogReportQueryDto q,
            CancellationToken ct);

        Task<MessageLogFacetsDto> GetFacetsAsync(
            Guid businessId,
            DateTime? fromUtc,
            CancellationToken ct);

    }
}
