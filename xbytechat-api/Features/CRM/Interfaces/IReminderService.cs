using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Dtos;

namespace xbytechat.api.Features.CRM.Interfaces
{
    public interface IReminderService
    {
        //For creating new reminder
        Task<ReminderDto> AddReminderAsync(Guid businessId, ReminderDto dto);

        //List all reminders for dashboard view
        Task<IEnumerable<ReminderDto>> GetAllRemindersAsync(Guid businessId);

        //For loading reminder in edit mode
        Task<ReminderDto?> GetReminderByIdAsync(Guid businessId, Guid reminderId);

        //Handles editing
        Task<bool> UpdateReminderAsync(Guid businessId, Guid reminderId, ReminderDto dto);
        //Soft delete → IsActive = false
        Task<bool> DeleteReminderAsync(Guid businessId, Guid reminderId);
    }
}
