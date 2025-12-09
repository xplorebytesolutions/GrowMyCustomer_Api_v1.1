using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.MessageManagement.Services;

namespace xbytechat.api.Features.MessageManagement.Controllers
{
    [ApiController]
    [Route("api/message-status")]
    public class MessageStatusController : ControllerBase
    {
        private readonly IMessageStatusService _service;
        private readonly ILogger<MessageStatusController> _logger;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        public MessageStatusController(
    IMessageStatusService service,
    ILogger<MessageStatusController> logger,
    AppDbContext context,
    IConfiguration config)
        {
            _service = service;
            _logger = logger;
            _context = context;
            _config = config;
        }

        // ✅ STEP 1: Webhook Verification (GET)
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                    [FromQuery(Name = "hub.verify_token")] string token,
                                    [FromQuery(Name = "hub.challenge")] string challenge)
        {
            //var VERIFY_TOKEN = _config["WhatsApp:MetaToken"]; // ✅ pulled from config

            if (mode == "subscribe" && token == "xbytechat-secret-token")
            {
                _logger.LogInformation("✅ Webhook verified.");
                return Ok(challenge);
            }

            _logger.LogWarning("❌ Webhook verification failed.");
            return Forbid();
        }


        // ✅ STEP 2: Webhook Payload (POST)
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveStatus([FromBody] WebhookStatusDto dto)
        {
            if (dto == null || dto.statuses == null || dto.statuses.Count == 0)
            {
                _logger.LogWarning("⚠️ Invalid webhook payload received.");
                return BadRequest("Invalid payload");
            }

            await _service.LogWebhookStatusAsync(dto);
            _logger.LogInformation("✅ Webhook status processed successfully.");

            return Ok(new { success = true });
        }

        // ✅ STEP 3: Frontend UI (GET Logs)
        [HttpGet]
        public async Task<IActionResult> GetStatusLogs([FromQuery] Guid businessId)
        {
            var logs = await _context.MessageStatusLogs// MessageStatusLogs
                .Where(x => x.BusinessId == businessId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .Select(x => new
                {
                    x.MessageId,
                    x.RecipientNumber,
                    x.Status,
                    x.SentAt,
                    x.DeliveredAt,
                    x.ReadAt,
                    x.ErrorMessage,
                    x.TemplateCategory,
                    x.MessageType
                })
                .ToListAsync();

            return Ok(new { success = true, data = logs });
        }
    }
}
