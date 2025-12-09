using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RemindersController : ControllerBase
    {
        private readonly IReminderService _reminderService;

        public RemindersController(IReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        [HttpPost]
        public async Task<IActionResult> AddReminder(ReminderDto dto)
        {
            try
            {
                var businessId = HttpContext.User.GetBusinessId();
                if (dto == null)
                    return BadRequest(ResponseResult.ErrorInfo("Reminder data is missing."));

                var result = await _reminderService.AddReminderAsync(businessId, dto);
                return Ok(ResponseResult.SuccessInfo("Reminder created.", result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("An error occurred while adding the reminder.", ex.Message));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllReminders()
        {
            try
            {
                var businessId = HttpContext.User.GetBusinessId();
                var reminders = await _reminderService.GetAllRemindersAsync(businessId);
                return Ok(ResponseResult.SuccessInfo("Reminders loaded.", reminders));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ResponseResult.ErrorInfo("An error occurred while fetching reminders.", ex.Message));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReminderById(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var reminder = await _reminderService.GetReminderByIdAsync(businessId, id);
            if (reminder == null)
                return NotFound(ResponseResult.ErrorInfo("Reminder not found."));
            return Ok(ResponseResult.SuccessInfo("Reminder loaded.", reminder));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReminder(Guid id, [FromBody] ReminderDto dto)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _reminderService.UpdateReminderAsync(businessId, id, dto);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Reminder not found."));
            return Ok(ResponseResult.SuccessInfo("Reminder updated."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReminder(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _reminderService.DeleteReminderAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Reminder not found."));
            return Ok(ResponseResult.SuccessInfo("Reminder deleted."));
        }
    }
}
