using System;
using System.Threading.Tasks;
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

        // 🧩 Log Note Added into Timeline
        public async Task<bool> LogNoteAddedAsync(CRMTimelineLogDto dto)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = "NoteAdded",
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

        // ⏰ Log Reminder Set into Timeline
        public async Task<bool> LogReminderSetAsync(CRMTimelineLogDto dto)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = "ReminderSet",
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

        // 🏷️ Log Tag Applied into Timeline
        public async Task<bool> LogTagAppliedAsync(CRMTimelineLogDto dto)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = "TagApplied",
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
