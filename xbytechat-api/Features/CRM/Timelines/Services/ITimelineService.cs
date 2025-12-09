using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Timelines.DTOs;

namespace xbytechat.api.Features.CRM.Timelines.Services
{
    public interface ITimelineService
    {
        // Already existing methods...

        // 🆕 CRM related methods
        Task<bool> LogNoteAddedAsync(CRMTimelineLogDto dto);
        Task<bool> LogReminderSetAsync(CRMTimelineLogDto dto);
        Task<bool> LogTagAppliedAsync(CRMTimelineLogDto dto);
    }
}
