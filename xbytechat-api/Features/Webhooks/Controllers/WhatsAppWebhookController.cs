using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.Controllers
{
    [ApiController]
    [Route("api/webhooks/whatsapp")]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly ILogger<WhatsAppWebhookController> _logger;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly IWhatsAppWebhookService _webhookService;
        private readonly IWebhookQueueService _queue;
        private readonly IFailedWebhookLogService _failedWebhookLogService;

        public WhatsAppWebhookController(
            ILogger<WhatsAppWebhookController> logger,
            IConfiguration config,
            AppDbContext context,
            IWhatsAppWebhookService webhookService,
            IWebhookQueueService queue,
            IFailedWebhookLogService failedWebhookLogService)
        {
            _logger = logger;
            _config = config;
            _context = context;
            _webhookService = webhookService;
            _queue = queue;
            _failedWebhookLogService = failedWebhookLogService;
        }

        // ✅ Step 1: Meta verification endpoint (GET)
        // Meta calls this to verify your webhook with hub.verify_token and expects you to return hub.challenge
        [HttpGet]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            _logger.LogInformation(
                "🔎 WhatsApp webhook verification request received. mode={Mode}, tokenLength={TokenLength}",
                mode,
                string.IsNullOrEmpty(token) ? 0 : token.Length);

            // 🔐 Load your secret token from config or environment.
            // Support multiple keys for safety/backward-compat:
            //  - WhatsApp:MetaVerifyToken  (matches your appsettings)
            //  - WhatsApp:MetaToken        (older name)
            //  - WhatsApp:VerifyWebHookToken (what this code was using)
            var expectedToken = _config["WhatsApp:VerifyWebHookToken"];

            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                _logger.LogError(
                    "❌ WhatsApp webhook verification failed: no verify token configured. " +
                    "Set WhatsApp:MetaVerifyToken (or WhatsApp:MetaToken / WhatsApp:VerifyWebHookToken) in configuration.");
                return Forbid("Server verify token not configured.");
            }

            if (mode == "subscribe" && token == expectedToken)
            {
                _logger.LogInformation("✅ WhatsApp webhook verified successfully.");
                return Ok(challenge); // Meta expects a 200 OK with the challenge value
            }

            _logger.LogWarning(
                "❌ WhatsApp webhook verification failed. Mode={Mode}, token did not match configured value.",
                mode);
            return Forbid("Token mismatch.");
        }

       

        [HttpPost]
        public async Task<IActionResult> HandleStatus([FromBody] JsonElement payload)
        {
            try
            {
                var bodyString = payload.ToString();

                _logger.LogInformation(
                    "📥 WhatsApp webhook POST received. Path={Path}, Query={Query}, Length={Length} chars.",
                    HttpContext.Request.Path,
                    HttpContext.Request.QueryString.ToString(),
                    bodyString?.Length ?? 0
                );

                var cloned = payload.Clone();
                var enqueued = _queue.Enqueue(cloned);
                if (!enqueued)
                {
                    await _failedWebhookLogService.LogFailureAsync(new FailedWebhookLogDto
                    {
                        FailureType = "QueueOverload",
                        SourceModule = nameof(WhatsAppWebhookController),
                        ErrorMessage = "Webhook queue full; payload dropped.",
                        RawJson = payload.GetRawText(),
                        CreatedAt = DateTime.UtcNow
                    });

                    _logger.LogWarning("⚠️ WhatsApp webhook queue overload. Payload persisted to FailedWebhookLogs.");
                    return Ok(new { received = true });
                }

                _logger.LogInformation("📥 Webhook payload enqueued successfully.");
                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to enqueue WhatsApp webhook payload.");
                return StatusCode(500, new { error = "Webhook queue failed" });
            }
        }

    }
}


