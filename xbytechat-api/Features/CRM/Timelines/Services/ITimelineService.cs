using xbytechat.api.Features.CRM.Timelines.DTOs;

namespace xbytechat.api.Features.CRM.Timelines.Services
{
    public interface ITimelineService
    {
        Task<bool> LogNoteAddedAsync(CRMTimelineLogDto dto);
        Task<bool> LogReminderSetAsync(CRMTimelineLogDto dto);
        Task<bool> LogTagAppliedAsync(CRMTimelineLogDto dto);

        // ✅ NEW
        Task<bool> LogReminderUpdatedAsync(CRMTimelineLogDto dto);
        Task<bool> LogReminderDeletedAsync(CRMTimelineLogDto dto);

        Task<bool> LogTagRemovedAsync(CRMTimelineLogDto dto);
    }
}
