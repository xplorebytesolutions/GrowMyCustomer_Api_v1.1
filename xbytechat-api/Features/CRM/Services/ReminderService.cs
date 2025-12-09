using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Mappers;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CRM.Services
{
    public class ReminderService : IReminderService
    {
        private readonly AppDbContext _db;

        public ReminderService(AppDbContext db)
        {
            _db = db;
        }

        //public async Task<ReminderDto> AddReminderAsync(Guid businessId, ReminderDto dto)
        //{
        //    var reminder = new Reminder
        //    {
        //        Id = Guid.NewGuid(),
        //        BusinessId = businessId,
        //        //ContactId = dto.ContactId,
        //        Title = dto.Title,
        //        Description = dto.Description,
        //        DueAt = dto.DueAt,
        //        Status = dto.Status ?? "Pending",
        //        ReminderType = dto.ReminderType,
        //        Priority = dto.Priority,
        //        IsRecurring = dto.IsRecurring,
        //        RecurrencePattern = dto.RecurrencePattern,
        //        SendWhatsappNotification = dto.SendWhatsappNotification,
        //        LinkedCampaign = dto.LinkedCampaign,
        //        CreatedAt = DateTime.UtcNow,
        //        IsActive = true
        //    };

        //    _db.Reminders.Add(reminder);
        //    await _db.SaveChangesAsync();

        //    return MapToDto(reminder);
        //}
        public async Task<ReminderDto> AddReminderAsync(Guid businessId, ReminderDto dto)
        {
            try
            {
                var reminder = new Reminder
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = dto.ContactId ?? Guid.Empty, // add default fallback
                    Title = dto.Title,
                    Description = dto.Description,
                    DueAt = DateTime.SpecifyKind(dto.DueAt, DateTimeKind.Utc),
                    Status = dto.Status ?? "Pending",
                    ReminderType = dto.ReminderType,
                    Priority = dto.Priority,
                    IsRecurring = dto.IsRecurring,
                    RecurrencePattern = dto.RecurrencePattern,
                    SendWhatsappNotification = dto.SendWhatsappNotification,
                    LinkedCampaign = dto.LinkedCampaign,
                    CreatedAt = DateTime.SpecifyKind(dto.DueAt, DateTimeKind.Utc),
                    IsActive = true
                };

                _db.Reminders.Add(reminder);
                await _db.SaveChangesAsync();

                return MapToDto(reminder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error in AddReminderAsync: " + ex.Message);
                throw;
            }
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
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);

            return reminder == null ? null : MapToDto(reminder);
        }

        public async Task<bool> UpdateReminderAsync(Guid businessId, Guid reminderId, ReminderDto dto)
        {
            var reminder = await _db.Reminders.FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);
            if (reminder == null) return false;

            reminder.Title = dto.Title;
            reminder.Description = dto.Description;
            reminder.DueAt = DateTime.SpecifyKind(dto.DueAt, DateTimeKind.Utc);
            reminder.Status = dto.Status ?? reminder.Status;
            reminder.ReminderType = dto.ReminderType;
            reminder.Priority = dto.Priority;
            reminder.IsRecurring = dto.IsRecurring;
            reminder.RecurrencePattern = dto.RecurrencePattern;
            reminder.SendWhatsappNotification = dto.SendWhatsappNotification;
            reminder.LinkedCampaign = dto.LinkedCampaign;
            reminder.UpdatedAt = DateTime.UtcNow;

            if (dto.Status?.ToLower() == "done")
                reminder.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReminderAsync(Guid businessId, Guid reminderId)
        {
            var reminder = await _db.Reminders.FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == reminderId && r.IsActive);
            if (reminder == null) return false;

            reminder.IsActive = false;
            await _db.SaveChangesAsync();
            return true;
        }

        private ReminderDto MapToDto(Reminder r)
        {
            return new ReminderDto
            {
                Id = r.Id,
                ContactId = r.ContactId,
                Title = r.Title,
                Description = r.Description,
                DueAt = r.DueAt,
                Status = r.Status,
                ReminderType = r.ReminderType,
                Priority = r.Priority,
                IsRecurring = r.IsRecurring,
                RecurrencePattern = r.RecurrencePattern,
                SendWhatsappNotification = r.SendWhatsappNotification,
                LinkedCampaign = r.LinkedCampaign,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CompletedAt = r.CompletedAt,
                IsActive = r.IsActive
            };
        }
    }
}
