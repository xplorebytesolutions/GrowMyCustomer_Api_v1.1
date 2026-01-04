using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Models;

namespace xbytechat.api.Features.CRM.Timelines.Services
{
    public class TimelineService : ITimelineService
    {
        private readonly AppDbContext _context;

        public TimelineService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> LogNoteAddedAsync(CRMTimelineLogDto dto)
            => await InsertAsync(dto, "NoteAdded", dto.Description);

        public async Task<bool> LogReminderSetAsync(CRMTimelineLogDto dto)
            => await InsertAsync(dto, "ReminderSet", dto.Description);

        public async Task<bool> LogTagAppliedAsync(CRMTimelineLogDto dto)
            => await InsertAsync(dto, "TagApplied", dto.Description);

        // ✅ NEW
        public async Task<bool> LogReminderUpdatedAsync(CRMTimelineLogDto dto)
            => await InsertAsync(dto, "ReminderUpdated", dto.Description);

        // ✅ NEW
        public async Task<bool> LogReminderDeletedAsync(CRMTimelineLogDto dto)
            => await InsertAsync(dto, "ReminderDeleted", dto.Description);

        private async Task<bool> InsertAsync(CRMTimelineLogDto dto, string eventType, string description)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = eventType,
                    Description = description,
                    ReferenceId = dto.ReferenceId,
                    CreatedBy = dto.CreatedBy,
                    Source = "CRM",
                    Category = dto.Category ?? "CRM",
                    CreatedAt = dto.Timestamp ?? DateTime.UtcNow,
                    IsSystemGenerated = false
                };

                _context.LeadTimelines.Add(timeline);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> LogTagRemovedAsync(CRMTimelineLogDto dto)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = "TagRemoved",
                    Description = dto.Description,
                    ReferenceId = dto.ReferenceId,
                    CreatedBy = dto.CreatedBy,
                    Source = "CRM",
                    Category = dto.Category ?? "CRM",
                    CreatedAt = dto.Timestamp ?? DateTime.UtcNow,
                    IsSystemGenerated = false
                };

                _context.LeadTimelines.Add(timeline);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
