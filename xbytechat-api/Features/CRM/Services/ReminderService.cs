using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Mappers;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CRM.Services
{
    public class ReminderService : IReminderService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(AppDbContext db, ITimelineService timelineService, ILogger<ReminderService> logger)
        {
            _db = db;
            _timelineService = timelineService;
            _logger = logger;
        }

        public async Task<ReminderDto> AddReminderAsync(Guid businessId, ReminderDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var title = string.IsNullOrWhiteSpace(dto.Title) ? "(No title)" : dto.Title.Trim();
            var status = string.IsNullOrWhiteSpace(dto.Status) ? "Pending" : dto.Status.Trim();

            // ✅ Your DB model uses Guid (not nullable), so we must choose a value
            var contactId = dto.ContactId ?? Guid.Empty;

            var reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ContactId = contactId, // ✅ Guid value (Guid.Empty if none)
                Title = title,
                Description = dto.Description?.Trim(),
                DueAt = EnsureUtc(dto.DueAt),
                Status = status,
                ReminderType = dto.ReminderType?.Trim(),
                Priority = dto.Priority,
                IsRecurring = dto.IsRecurring,
                RecurrencePattern = dto.RecurrencePattern,
                SendWhatsappNotification = dto.SendWhatsappNotification,
                LinkedCampaign = dto.LinkedCampaign,
                CreatedAt = DateTime.UtcNow, // ✅ correct
                UpdatedAt = null,
                CompletedAt = IsDoneStatus(status) ? DateTime.UtcNow : null,
                IsActive = true
            };

            _db.Reminders.Add(reminder);
            await _db.SaveChangesAsync();

            // ✅ Timeline only if contactId is real
            if (contactId != Guid.Empty)
            {
                try
                {
                    await _timelineService.LogReminderSetAsync(new CRMTimelineLogDto
                    {
                        ContactId = contactId,
                        BusinessId = businessId,
                        EventType = "ReminderSet",
                        Description = $"⏰ Reminder set: {reminder.Title} (Due: {reminder.DueAt:yyyy-MM-dd HH:mm} UTC)",
                        ReferenceId = reminder.Id,
                        CreatedBy = dto.CreatedBy ?? "system",
                        Timestamp = DateTime.UtcNow,
                        Category = "CRM"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Timeline log failed for ReminderId={ReminderId}", reminder.Id);
                }
            }

            return ReminderMapper.MapToDto(reminder);
        }

        public async Task<IEnumerable<ReminderDto>> GetAllRemindersAsync(Guid businessId)
        {
            return await _db.Reminders
                .AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.IsActive)
                .OrderBy(r => r.DueAt)
                .Select(r => ReminderMapper.MapToDto(r))
                .ToListAsync();
        }

        public async Task<ReminderDto?> GetReminderByIdAsync(Guid businessId, Guid reminderId)
        {
            var reminder = await _db.Reminders
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);

            return reminder == null ? null : ReminderMapper.MapToDto(reminder);
        }

        public async Task<bool> UpdateReminderAsync(Guid businessId, Guid reminderId, ReminderDto dto)
        {
            var reminder = await _db.Reminders
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);

            if (reminder == null) return false;

            if (!string.IsNullOrWhiteSpace(dto.Title))
                reminder.Title = dto.Title.Trim();

            reminder.Description = dto.Description?.Trim();
            reminder.DueAt = EnsureUtc(dto.DueAt);

            if (!string.IsNullOrWhiteSpace(dto.Status))
                reminder.Status = dto.Status.Trim();

            reminder.ReminderType = dto.ReminderType?.Trim();
            reminder.Priority = dto.Priority;
            reminder.IsRecurring = dto.IsRecurring;
            reminder.RecurrencePattern = dto.RecurrencePattern;
            reminder.SendWhatsappNotification = dto.SendWhatsappNotification;
            reminder.LinkedCampaign = dto.LinkedCampaign;
            reminder.UpdatedAt = DateTime.UtcNow;

            if (IsDoneStatus(reminder.Status))
                reminder.CompletedAt = reminder.CompletedAt ?? DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // (optional) timeline “updated”
            if (reminder.ContactId != Guid.Empty)
            {
                try
                {
                    await _timelineService.LogReminderUpdatedAsync(new CRMTimelineLogDto
                    {
                        ContactId = reminder.ContactId,
                        BusinessId = businessId,
                        EventType = "ReminderUpdated",
                        Description = $"✏️ Reminder updated: {reminder.Title}",
                        ReferenceId = reminder.Id,
                        CreatedBy = dto.CreatedBy ?? "system",
                        Timestamp = DateTime.UtcNow,
                        Category = "CRM"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Timeline log failed for ReminderUpdated ReminderId={ReminderId}", reminder.Id);
                }
            }

            return true;
        }

        // ✅ HARD DELETE
        public async Task<bool> DeleteReminderAsync(Guid businessId, Guid reminderId)
        {
            var reminder = await _db.Reminders
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);

            if (reminder == null) return false;

            var contactId = reminder.ContactId;

            _db.Reminders.Remove(reminder);
            await _db.SaveChangesAsync();

            if (contactId != Guid.Empty)
            {
                try
                {
                    await _timelineService.LogReminderDeletedAsync(new CRMTimelineLogDto
                    {
                        ContactId = contactId,
                        BusinessId = businessId,
                        EventType = "ReminderDeleted",
                        Description = $"🗑️ Reminder deleted: {reminder.Title}",
                        ReferenceId = reminder.Id,
                        CreatedBy = "system",
                        Timestamp = DateTime.UtcNow,
                        Category = "CRM"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Timeline log failed for ReminderDeleted ReminderId={ReminderId}", reminder.Id);
                }
            }

            return true;
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static bool IsDoneStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim().ToLowerInvariant();
            return s == "done" || s == "completed" || s == "complete";
        }
    }
}
