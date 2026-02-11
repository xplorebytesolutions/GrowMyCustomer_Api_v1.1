#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.DTOs;
using xbytechat.api.Features.ESU.Facebook.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.ESU.Facebook.Controllers
{
    [ApiController]
    [Route("api/esu/facebook")]
    [Authorize] // all endpoints below require authenticated workspace
    public sealed class FacebookEsuController : ControllerBase
    {
        private readonly IFacebookEsuService _service;
        private readonly IEsuStatusService _status;
        private readonly ILogger<FacebookEsuController> _log;
        private readonly string _uiBase;

        public FacebookEsuController(
            IFacebookEsuService service,
            IEsuStatusService status,
            ILogger<FacebookEsuController> log,
            IConfiguration cfg)
        {
            _service = service;
            _status = status;
            _log = log;
            _uiBase = (cfg["Ui:PublicBaseUrl"] ?? cfg["App:PublicBaseUrl"] ?? "http://localhost:3000/").TrimEnd('/');
        }

        private static string SanitizeReturnUrlOrDefault(string? returnUrl, string fallback)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return fallback;

            returnUrl = returnUrl.Trim();

            if (!returnUrl.StartsWith("/", StringComparison.Ordinal) ||
                returnUrl.StartsWith("//", StringComparison.Ordinal) ||
                returnUrl.Contains("\\", StringComparison.Ordinal))
            {
                return fallback;
            }

            return returnUrl;
        }

        private static string? TryExtractReturnUrlFromState(string state)
        {
            // state format: {bizId:N}|{unixTs}|{randomHex}|{returnUrl}
            // keep backward compatible: if parsing fails, return null.
            var parts = state.Split('|', 4, StringSplitOptions.None);
            if (parts.Length < 4) return null;

            var returnUrl = parts[3];
            return string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl;
        }

        private static string AppendParams(string path, string paramPairs)
        {
            if (string.IsNullOrWhiteSpace(paramPairs)) return path;

            var cleaned = paramPairs.Trim();
            cleaned = cleaned.TrimStart('?', '&');
            if (cleaned.Length == 0) return path;

            var sep = path.Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return $"{path}{sep}{cleaned}";
        }

        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
            => Ok(new { module = "ESU", provider = "FACEBOOK", status = "OK" });

        // -------- START / RESTART ESU --------

        [HttpPost("start")]
        [DisableRateLimiting] // ESU handshake must not be throttled
        public async Task<IActionResult> Start(
            [FromBody] FacebookEsuStartRequestDto? dto,
            CancellationToken ct)
        {
            try
            {
                var businessId = User.GetBusinessId();
                if (businessId == Guid.Empty)
                    return Unauthorized(new { ok = false, message = "Business context missing in token." });

                var res = await _service.StartAsync(businessId, dto?.ReturnUrlAfterSuccess, ct);

                _log.LogInformation(
                    "ESU start issued for business={BusinessId}, state={State}, expires={Expires}",
                    businessId, res.State, res.ExpiresAtUtc);

                return Ok(new
                {
                    ok = true,
                    data = new
                    {
                        authUrl = res.LaunchUrl,
                        state = res.State,
                        expiresAtUtc = res.ExpiresAtUtc
                    }
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU start failed.");
                return StatusCode(500, new { ok = false, message = "Failed to start Meta Embedded Signup." });
            }
        }

        // -------- OAUTH CALLBACK (PUBLIC) --------

        //[HttpGet("callback")]
        //[AllowAnonymous]
        //[DisableRateLimiting]
        //public async Task<IActionResult> Callback(
        //    [FromQuery] string? code,
        //    [FromQuery] string? state,
        //    CancellationToken ct)
        //{
        //    Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        //    Response.Headers["Pragma"] = "no-cache";

        //    string Target(string q) => $"{_uiBase}/app/welcomepage{q}";

        //    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        //    {
        //        _log.LogWarning("ESU callback missing parameters. codeNull={CodeNull} stateNull={StateNull}",
        //            string.IsNullOrWhiteSpace(code), string.IsNullOrWhiteSpace(state));
        //        return Redirect(Target("?error=missing_code_or_state"));
        //    }

        //    try
        //    {
        //        await _service.HandleCallbackAsync(code!, state!, ct);
        //        _log.LogInformation("ESU callback success for state={State}", state);
        //        //return Redirect(Target("?connected=1"));
        //        return Redirect(Target("?esuStatus=success"));
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.LogError(ex, "ESU callback failed for state={State}", state);
        //        // return Redirect(Target("?error=oauth_exchange_failed"));
        //        return Redirect(Target("?esuStatus=failed&error=oauth_exchange_failed"));
        //    }
        //}
        [HttpGet("callback")]
        [AllowAnonymous]
        [DisableRateLimiting]
        public async Task<IActionResult> Callback(
    [FromQuery] string? code,
    [FromQuery] string? state,
    CancellationToken ct)
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";

            var returnUrlFromState = !string.IsNullOrWhiteSpace(state)
                ? TryExtractReturnUrlFromState(state)
                : null;

            var returnPath = SanitizeReturnUrlOrDefault(returnUrlFromState, "/app/welcomepage");

            string Target(string paramPairs)
                => $"{_uiBase}{AppendParams(returnPath, paramPairs)}";

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                _log.LogWarning(
                    "ESU callback missing parameters. codeNull={CodeNull} stateNull={StateNull}",
                    string.IsNullOrWhiteSpace(code),
                    string.IsNullOrWhiteSpace(state));

                return Redirect(Target("esuStatus=failed&error=missing_code_or_state"));
            }

            try
            {
                await _service.HandleCallbackAsync(code!, state!, ct);
                _log.LogInformation("ESU callback success for state={State}", state);

                return Redirect(Target("esuStatus=success"));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU callback failed for state={State}", state);
                return Redirect(Target("esuStatus=failed&error=oauth_exchange_failed"));
            }
        }
        // -- Set two factor verification ---

    //    [HttpPost("register-number")]
    //    public async Task<IActionResult> RegisterPhoneNumber(
    //[FromBody] RegisterPhoneNumberDto dto,
    //CancellationToken ct)
    //    {
    //        var businessId = User.GetBusinessId();
    //        if (businessId == Guid.Empty)
    //            return Unauthorized();

    //        await _service.RegisterPhoneNumberAsync(
    //            businessId,
    //            dto.Pin,
    //            ct);

    //        return Ok(new { ok = true });
    //    }
        [HttpPost("register-number")]
        public async Task<IActionResult> RegisterPhoneNumber([FromBody] RegisterPhoneNumberDto dto, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            await _service.RegisterPhoneNumberAsync(businessId, dto.Pin, ct);
            return Ok(new { ok = true });
        }

        public sealed class RegisterPhoneNumberDto
        {
            public string Pin { get; set; } = default!; // 6 digits
        }

        // -------- DISCONNECT (FULL DEAUTHORIZE) --------

        [HttpDelete("disconnect")]
        public async Task<IActionResult> Disconnect(CancellationToken ct)
        {
            try
            {
                var businessId = User.GetBusinessId();
                if (businessId == Guid.Empty)
                    return Unauthorized(new { ok = false, message = "Business context missing in token." });

                await _service.DisconnectAsync(businessId, ct);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU disconnect failed.");
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Failed to disconnect WhatsApp for this workspace."
                });
            }
        }

        // -------- STATUS --------

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty)
                return Unauthorized(new { ok = false, message = "Business context missing in token." });

            var dto = await _status.GetStatusAsync(businessId, ct);

            // FE already supports both plain DTO and { ok, data }
            return Ok(new { ok = true, data = dto });
        }

        // inside FacebookEsuController

        [HttpDelete("hard-delete-full-account")]
        public async Task<IActionResult> DeleteAccountAndData(CancellationToken ct)
        {
            try
            {
                var businessId = User.GetBusinessId();
                if (businessId == Guid.Empty)
                    return Unauthorized(new { ok = false, message = "Business context missing in token." });

                await _service.FullDeleteAsync(businessId, ct);

                return Ok(new
                {
                    ok = true,
                    message = "WhatsApp Business API connection and related onboarding data have been deleted for this workspace."
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU account/data delete failed.");
                return StatusCode(500, new
                {
                    ok = false,
                    message = "Failed to delete WhatsApp onboarding data for this workspace."
                });
            }
        }

    }
}



