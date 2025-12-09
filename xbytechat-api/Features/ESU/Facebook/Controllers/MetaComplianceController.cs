#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Options;
using xbytechat.api.Features.ESU.Facebook.Services;

namespace xbytechat.api.Features.ESU.Facebook.Controllers
{
    [ApiController]
    [Route("meta")]
    public sealed class MetaComplianceController : ControllerBase
    {
        private readonly IFacebookEsuService _esuService;
        private readonly IOptions<FacebookOauthOptions> _fbOpts;
        private readonly ILogger<MetaComplianceController> _log;

        public MetaComplianceController(
            IFacebookEsuService esuService,
            IOptions<FacebookOauthOptions> fbOpts,
            ILogger<MetaComplianceController> log)
        {
            _esuService = esuService;
            _fbOpts = fbOpts;
            _log = log;
        }

        // Configure this URL in Meta's "Data Deletion" settings.
        // Meta sends `signed_request` (base64url.header.payload, HMAC-SHA256 with AppSecret).
        [HttpPost("data-deletion")]
        public async Task<IActionResult> HandleDataDeletion([FromForm] string signed_request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(signed_request))
                return BadRequest(new { status = "error", message = "missing signed_request" });

            var (ok, payloadJson) = TryValidateSignedRequest(signed_request);
            if (!ok)
                return BadRequest(new { status = "error", message = "invalid signed_request" });

            // Payload typically contains user_id or similar identifiers.
            // You must map that to your BusinessId based on how you tied ESU sessions to businesses.
            // For now we assume you store mapping elsewhere and resolve it here.
            var businessId = ResolveBusinessIdFromPayload(payloadJson);
            if (businessId == Guid.Empty)
            {
                _log.LogInformation("Meta data-deletion: no matching business for payload={Payload}", payloadJson);
                return Ok(new { status = "ignored" });
            }

            _log.LogInformation("Meta data-deletion: biz={BusinessId}", businessId);

            // Canonical cleanup: same as manual disconnect
            await _esuService.DisconnectAsync(businessId, ct);

            // Optional: enqueue deeper anonymization/purge if your policy requires.
            return Ok(new
            {
                status = "success",
                reference_id = businessId
            });
        }

        private (bool ok, string payloadJson) TryValidateSignedRequest(string signedRequest)
        {
            var parts = signedRequest.Split('.', 2);
            if (parts.Length != 2) return (false, "");

            var providedSig = Base64UrlDecode(parts[0]);
            var payloadBytes = Base64UrlDecode(parts[1]);

            var appSecret = _fbOpts.Value.AppSecret;
            if (string.IsNullOrWhiteSpace(appSecret))
                return (false, "");

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[1]));

            // Constant-time compare
            if (!CryptographicOperations.FixedTimeEquals(providedSig, expectedSig))
                return (false, "");

            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            return (true, payloadJson);
        }

        private static byte[] Base64UrlDecode(string input)
        {
            input = input.Replace('-', '+').Replace('_', '/');
            switch (input.Length % 4)
            {
                case 2: input += "=="; break;
                case 3: input += "="; break;
            }
            return Convert.FromBase64String(input);
        }

        // TODO: implement this mapping based on your stored ESU context.
        private Guid ResolveBusinessIdFromPayload(string payloadJson)
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            // Example: if you store mapping from Meta user_id/page_id/WABA to BusinessId.
            // This is intentionally left for your existing infra.
            // Return Guid.Empty when no mapping found.

            // var userId = root.GetProperty("user_id").GetString();
            // lookup Biz by userId...

            return Guid.Empty;
        }
    }
}
