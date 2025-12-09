using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.DTOs.Messages;
using xbytechat.api.Helpers;
using xbytechat.api.Services.Messages.Interfaces;

namespace xbytechat.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // TODO: refine with permission/quota checks (e.g., MESSAGING.SEND)
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;

        public MessagesController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        /// ✅ SEND TEXT MESSAGE
        [HttpPost("send-text")]
        public async Task<IActionResult> SendTextMessage([FromBody] TextMessageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var result = await _messageService.SendMessageAsync(dto);

            return result.Success
                ? Ok(new { success = true, message = result.Message, response = result.Data })
                : StatusCode(500, new { success = false, message = result.Message, error = result.ErrorMessage });
        }

        /// ✅ SEND IMAGE MESSAGE
        [HttpPost("send-image")]
        public async Task<IActionResult> SendImageMessage([FromBody] ImageMessageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var result = await _messageService.SendMessageAsync(dto);

            return result.Success
                ? Ok(new { success = true, message = result.Message, response = result.Data })
                : StatusCode(500, new { success = false, message = result.Message, error = result.ErrorMessage });
        }

        /// ✅ SEND TEMPLATE MESSAGE
        [HttpPost("send-template")]
        public async Task<IActionResult> SendTemplateMessage([FromBody] TemplateMessageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var result = await _messageService.SendMessageAsync(dto);

            return result.Success
                ? Ok(new { success = true, message = result.Message, response = result.Data })
                : StatusCode(500, new { success = false, message = result.Message, error = result.ErrorMessage });
        }

        /// ✅ SEND CTA BUTTON MESSAGE (Free-form Interactive)
        [HttpPost("send-cta")]
        public async Task<IActionResult> SendCtaMessage([FromBody] CtaMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RecipientPhone) || string.IsNullOrWhiteSpace(dto.BodyText) || dto.Buttons == null || dto.Buttons.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Invalid request — phone, body text and buttons are required"
                });
            }

            // Optional tracking
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString() ?? "unknown";

            var result = await _messageService.SendInteractiveMessageAsync(dto.RecipientPhone, dto.BodyText, dto.Buttons);

            return result.Success
                ? Ok(new
                {
                    success = true,
                    message = result.Message,
                    messageId = result.MessageId,
                    logId = result.MessageLogId,
                    raw = result.RawResponse
                })
                : StatusCode(500, new
                {
                    success = false,
                    message = result.Message,
                    error = result.ErrorMessage
                });
        }

        /// ✅ SEND BULK MESSAGES
        //[HttpPost("send-bulk")]
        //public async Task<SendResultExtended> SendBulkMessagesAsync(BulkMessageDto dto)
        //{
        //    var result = new SendResultExtended
        //    {
        //        Success = true,
        //        Message = "✅ All messages processed.",
        //        LogId = null,
        //        MessageId = null
        //    };

        //    try
        //    {
        //        foreach (var contactId in dto.ContactIds)
        //        {
        //            var contact = await _dbContext.Contacts
        //                .Include(c => c.Business)
        //                .FirstOrDefaultAsync(c => c.Id == contactId);

        //            if (contact == null || string.IsNullOrWhiteSpace(contact.PhoneNumber))
        //                continue;

        //            BaseMessageDto message;

        //            if (dto.MessageType.ToLower() == "template")
        //            {
        //                message = new TemplateMessageDto
        //                {
        //                    RecipientNumber = contact.PhoneNumber,
        //                    MessageContent = dto.MessageTemplate,
        //                    MessageType = "template",
        //                    TemplateName = dto.TemplateName!,
        //                    TemplateParameters = dto.TemplateParams ?? new List<string>(),
        //                    BusinessId = contact.BusinessId
        //                };
        //            }
        //            else
        //            {
        //                message = new TextMessageDto
        //                {
        //                    RecipientNumber = contact.PhoneNumber,
        //                    MessageContent = dto.MessageTemplate,
        //                    MessageType = "text",
        //                    BusinessId = contact.BusinessId
        //                };
        //            }

        //            await SendMessageAsync(message); // already returns SendResultExtended
        //        }

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        return new SendResultExtended
        //        {
        //            Success = false,
        //            Message = "❌ Bulk send failed.",
        //            ErrorMessage = ex.Message
        //        };
        //    }
        //}


    }
}
