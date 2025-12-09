using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.CustomeApi.Auth;
using xbytechat.api.Features.CustomeApi.DTOs;
using xbytechat.api.Features.CustomeApi.Services;

namespace xbytechat.api.Features.CustomeApi.Controllers
{
    [ApiController]
    [Route("api/custom")]
    public sealed class CustomApiController : ControllerBase
    {
        private readonly ICustomApiService _service;
        private readonly StaticApiKeyOptions _api;
        private readonly CtaJourneyPublisher _journeyPublisher;
        public CustomApiController(ICustomApiService service, IOptions<StaticApiKeyOptions> api, CtaJourneyPublisher journeyPublisher)
        {
            _service = service;
            _api = api.Value;
            _journeyPublisher = journeyPublisher;
        }

        /// <summary>
        /// Sends a WhatsApp template (optionally with VIDEO header) by phoneNumberId.
        /// Body: { phoneNumberId, to, templateId, variables:{ "1":"..." }, videoUrl, flowConfigId }
        /// </summary>
        [HttpPost("sendflow")]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> SendTemplate([FromBody] DirectTemplateSendRequest req, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "❌ Invalid request body.", errors = ModelState });

            // Minimal shared-secret auth
            var provided = Request.Headers["X-Auth-Key"].FirstOrDefault()
                           ?? Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(_api.Key) ||
                string.IsNullOrWhiteSpace(provided) ||
                !string.Equals(provided, _api.Key, System.StringComparison.Ordinal))
            {
                return Unauthorized(new { success = false, message = "🔒 Invalid or missing key." });
            }

            var result = await _service.SendTemplateAsync(req, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpPost("test-webhook")]
        public async Task<IActionResult> TestWebhook([FromQuery] Guid businessId, CancellationToken ct)
        {
            var (ok, msg) = await _journeyPublisher.ValidateAndPingAsync(businessId, ct);
            return ok ? Ok(new { ok, message = msg }) : BadRequest(new { ok, message = msg });
        }
    }
}
