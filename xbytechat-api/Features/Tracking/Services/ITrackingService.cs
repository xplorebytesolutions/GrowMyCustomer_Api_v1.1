using System.Threading.Tasks;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Tracking.Services
{
    public interface ITrackingService
    {
        Task LogCTAClickAsync(TrackingLogDto dto);
        Task<TrackingLogDetailsDto?> GetLogDetailsAsync(Guid logId);
        Task<ResponseResult> LogCTAClickWithEnrichmentAsync(TrackingLogDto dto);
        Task<List<TrackingLog>> GetFlowClickLogsAsync(Guid businessId);
    }
}
