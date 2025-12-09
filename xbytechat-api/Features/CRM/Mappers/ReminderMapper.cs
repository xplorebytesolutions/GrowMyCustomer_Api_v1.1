namespace xbytechat.api.Features.CRM.Mappers
{
    using xbytechat.api.Features.CRM.Dtos;
    using xbytechat.api.Features.CRM.Models;

    public static class ReminderMapper
    {
        public static ReminderDto MapToDto(Reminder r)
        {
            return new ReminderDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                DueAt = r.DueAt,
                ReminderType = r.ReminderType,
                Priority = r.Priority,
                IsRecurring = r.IsRecurring,
                RecurrencePattern = r.RecurrencePattern,
                SendWhatsappNotification = r.SendWhatsappNotification,
                LinkedCampaign = r.LinkedCampaign,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ContactId = r.ContactId
            };
        }
    }
}
