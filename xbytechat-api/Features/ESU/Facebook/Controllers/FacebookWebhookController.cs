using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Options;

namespace xbytechat.api.Features.ESU.Facebook.Controllers
{
    [ApiController]
    [Route("api/esu/facebook/webhook")]
    public sealed class FacebookWebhookController : ControllerBase
    {
        private readonly string _verifyToken;

        public FacebookWebhookController(IOptions<FacebookOptions> opts)
        {
            _verifyToken = opts.Value.VerifyToken ?? string.Empty;
        }

        // GET verify: echo hub.challenge if token matches
        [HttpGet]
        public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode,
                                    [FromQuery(Name = "hub.verify_token")] string? token,
                                    [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(token) &&
                token == _verifyToken &&
                !string.IsNullOrWhiteSpace(challenge))
            {
                return Content(challenge!, "text/plain");
            }
            return Forbid();
        }

        // POST stub: logs or routes ESU-related events (optional for App Review)
        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            // TODO: route event to your logger/queue if needed
            return Ok(new { ok = true });
        }
    }
}
