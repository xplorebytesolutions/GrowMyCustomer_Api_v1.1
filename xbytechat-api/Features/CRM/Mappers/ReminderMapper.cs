using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CRM.Mappers
{
    public static class ReminderMapper
    {
        public static ReminderDto MapToDto(Reminder r)
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
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CompletedAt = r.CompletedAt
            };
        }
    }
}
