using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Services;
using System.Threading.Tasks;

namespace xbytechat.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // TODO: add RequirePermission("MESSAGING.SEND") and quota enforcement
    public class WhatsAppController : ControllerBase
    {
        private readonly WhatsAppService _whatsAppService;

        public WhatsAppController(WhatsAppService whatsAppService)
        {
            _whatsAppService = whatsAppService;
        }

        /// <summary>
        /// Endpoint to send a WhatsApp message.
        /// </summary>
        /// <param name="recipientPhone">Recipient's phone number (including country code).</param>
        /// <param name="messageText">Text message to send.</param>
        /// <returns>Response with the result of the send operation.</returns>
        [HttpPost("send")]
        //[RequirePermission("MESSAGING.SEND")] // TODO: enable after mapping codes
        public async Task<IActionResult> SendMessage([FromQuery] string recipientPhone, [FromQuery] string messageText)
        {
            if (string.IsNullOrEmpty(recipientPhone) || string.IsNullOrEmpty(messageText))
            {
                return BadRequest(new { success = false, message = "Phone number and message text are required." });
            }

            // Call WhatsApp service to send the message
            var result = await _whatsAppService.SendMessageAsync(recipientPhone, messageText);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = "✅ Message sent successfully.",
                    response = result.RawResponse
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = result.ErrorMessage ?? "❌ Failed to send message.",
                    response = result.RawResponse
                });
            }
        }
    }
}
