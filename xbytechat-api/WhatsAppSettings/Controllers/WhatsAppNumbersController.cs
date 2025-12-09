using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.WhatsAppSettings.Services;
using xbytechat.api.Features.WhatsAppSettings.Models;
using xbytechat.api.Shared;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.WhatsAppSettings.Controllers
{
    [ApiController]
    [Route("api/whatsappsettings/{provider}/numbers")]
    public class WhatsAppNumbersController : ControllerBase
    {
        private readonly IWhatsAppPhoneNumberService _svc;
        private readonly ILogger<WhatsAppNumbersController> _logger;

        public WhatsAppNumbersController(
            IWhatsAppPhoneNumberService svc,
            ILogger<WhatsAppNumbersController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        // Helper to resolve BusinessId from claims/header/query (adjust to your auth)
        private bool TryGetBusinessId(out Guid businessId)
        {
            businessId = Guid.Empty;

            // 1) Claim (preferred)
            var claim = User?.FindFirst("BusinessId") ?? User?.FindFirst("businessId");
            if (claim != null && Guid.TryParse(claim.Value, out businessId))
                return true;

            // 2) Header fallback
            if (Request.Headers.TryGetValue("X-Business-Id", out var h)
                && Guid.TryParse(h.ToString(), out businessId))
                return true;

            // 3) Query fallback
            if (Guid.TryParse(HttpContext.Request.Query["businessId"], out businessId))
                return true;

            return false;
        }

        // GET /api/whatsappsettings/{provider}/numbers
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List([FromRoute] string provider)
        {
            if (!TryGetBusinessId(out var businessId))
                return BadRequest("BusinessId is required (claim/header/query).");

            var items = await _svc.ListAsync(businessId, provider);
            return Ok(items);
        }

        public sealed class UpsertRequest
        {
            public string PhoneNumberId { get; set; } = null!;
            public string WhatsAppBusinessNumber { get; set; } = null!;
            public string? SenderDisplayName { get; set; }
            public bool? IsActive { get; set; }
            public bool? IsDefault { get; set; }
        }

        // POST /api/whatsappsettings/{provider}/numbers
        [HttpPost("")]
        [ProducesResponseType(typeof(WhatsAppPhoneNumber), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
       // [HttpPost("")]
        public async Task<IActionResult> Upsert([FromRoute] string provider, [FromBody] UpsertRequest req)
        {
            if (!TryGetBusinessId(out var businessId))
                return BadRequest("BusinessId is required (claim/header/query).");

            if (string.IsNullOrWhiteSpace(req.PhoneNumberId))
                return BadRequest("phoneNumberId is required.");
            if (string.IsNullOrWhiteSpace(req.WhatsAppBusinessNumber))
                return BadRequest("whatsAppBusinessNumber is required.");

            try
            {
                var saved = await _svc.UpsertAsync(
                    businessId,
                    provider, // service normalizes
                    req.PhoneNumberId,
                    req.WhatsAppBusinessNumber,
                    req.SenderDisplayName,
                    req.IsActive,
                    req.IsDefault
                );
                return Ok(saved);
            }
            catch (InvalidOperationException ex) when (ex.InnerException is Npgsql.NpgsqlException npg)
            {
                // Bubble up constraint codes if useful
                // 23505 unique_violation, 23503 foreign_key_violation, 23502 not_null_violation
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    statusCode = 409,
                    code = npg.SqlState,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    statusCode = 500,
                    message = ex.Message
                });
            }
        }

        // DELETE /api/whatsappsettings/{provider}/numbers/{id}
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete([FromRoute] string provider, [FromRoute] Guid id)
        {
            if (!TryGetBusinessId(out var businessId))
                return BadRequest("BusinessId is required (claim/header/query).");

            var ok = await _svc.DeleteAsync(businessId, provider, id);
            if (!ok) return NotFound();

            return NoContent();
        }

        // PATCH /api/whatsappsettings/{provider}/numbers/{id}/default
        [HttpPatch("{id:guid}/default")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetDefault([FromRoute] string provider, [FromRoute] Guid id)
        {
            if (!TryGetBusinessId(out var businessId))
                return BadRequest("BusinessId is required (claim/header/query).");

            var ok = await _svc.SetDefaultAsync(businessId, provider, id);
            if (!ok) return NotFound();

            return NoContent();
        }

       
   

    }
}
