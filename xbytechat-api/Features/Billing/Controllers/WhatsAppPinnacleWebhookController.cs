using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using xbytechat_api.Features.Billing.Services;

namespace xbytechat_api.Features.Billing.Controllers
{
    [ApiController]
    [Route("api/webhooks/whatsapp/pinnacle")]
    public class WhatsAppPinnacleWebhookController : ControllerBase
    {
        private readonly ILogger<WhatsAppPinnacleWebhookController> _log;
        private readonly IBillingIngestService _ingest;

        public WhatsAppPinnacleWebhookController(
            ILogger<WhatsAppPinnacleWebhookController> log,
            IBillingIngestService ingest)
        {
            _log = log;
            _ingest = ingest;
        }

        // POST /api/webhooks/whatsapp/pinnacle?businessId=...
        [HttpPost]
        public async Task<IActionResult> Post([FromQuery] Guid businessId)
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();

            _log.LogInformation("Pinnacle webhook payload ({Len} chars) for Biz {Biz}", payload?.Length ?? 0, businessId);
            await _ingest.IngestFromWebhookAsync(businessId, "PINNACLE", payload);

            return Ok();
        }
    }
}
