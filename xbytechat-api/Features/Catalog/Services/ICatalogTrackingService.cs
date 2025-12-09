using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Catalog.Services
{
    public interface ICatalogTrackingService
    {
        Task<ResponseResult> LogClickAsync(CatalogClickLogDto dto);
        Task<ResponseResult> GetRecentLogsAsync(int limit);
    }
}
