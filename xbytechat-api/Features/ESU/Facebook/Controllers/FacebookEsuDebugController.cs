#nullable enable
using System;
using System.Collections.Generic; // <-- needed for the /me call
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.DTOs;
using xbytechat.api.Infrastructure;
using xbytechat.api.Shared; // AppDbContext

namespace xbytechat.api.Features.ESU.Facebook.Controllers
{
    [ApiController]
    [Route("api/esu/facebook/debug")]
   // [Authorize(Roles = "SuperAdmin")] // ⚠️ TEMPORARY for verification; lock down/remove after testing
    public sealed class FacebookEsuDebugController : ControllerBase
    {
        private readonly IFacebookTokenService _tokenService;
        private readonly AppDbContext _db;
        private readonly ILogger<FacebookEsuDebugController> _log;
        private readonly IFacebookGraphClient _graph;
        private readonly IEsuStatusService _status;
        public FacebookEsuDebugController(
            IFacebookTokenService tokenService,
            AppDbContext db,
            ILogger<FacebookEsuDebugController> log,
            IFacebookGraphClient graph,
            IEsuStatusService status)
        {
            _tokenService = tokenService;
            _db = db;
            _log = log;
            _graph = graph;
            _status = status;
        }

        // GET /api/esu/facebook/debug/token?businessId=...
        [HttpGet("token")]
        public async Task<IActionResult> GetToken([FromQuery] Guid businessId, CancellationToken ct)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            var t = await _tokenService.TryGetValidAsync(businessId, ct);
            if (t is null)
            {
                return Ok(new
                {
                    ok = false,
                    message = "No valid token found (missing or expired). Re-run ESU."
                });
            }

            return Ok(new
            {
                ok = true,
                tokenPreview = Mask(t.AccessToken),
                expiresAtUtc = t.ExpiresAtUtc,
                willExpireSoon = t.WillExpireSoon(),  // default 5m skew
                rawJsonLength = t.RawJson?.Length ?? 0
            });
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<ActionResult<EsuStatusDto>> GetStatus(CancellationToken ct)
        {
            // Uses the "businessId" claim from JWT (lowercase)
            var businessId = User.GetBusinessId();

            var dto = await _status.GetStatusAsync(businessId, ct);

            // Keep it simple; frontend already normalizes shape
            return Ok(dto);
        }


        [HttpPost("deauthorize")]
        public async Task<IActionResult> Deauthorize([FromQuery] Guid businessId, CancellationToken ct)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");
            await _status.DeauthorizeAsync(businessId, ct);
            return Ok(new { ok = true });
        }


        // GET /api/esu/facebook/debug/flags?businessId=...
        [HttpGet("flags")]
        public async Task<IActionResult> ListFlags([FromQuery] Guid businessId, CancellationToken ct)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            // Your IntegrationFlags model is the single-row, column-style model
            var row = await _db.IntegrationFlags
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);

            if (row is null)
            {
                return Ok(new { ok = true, count = 0, items = Array.Empty<object>() });
            }

            // Box each element as object so the array can be typed object[]
            var items = new object[]
            {
                new { key = "FACEBOOK_ESU_COMPLETED",value = row.FacebookEsuCompleted ? "true" : "false" }
    
            };

            return Ok(new { ok = true, count = items.Length, items });
        }

        private static string Mask(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= 10) return new string('*', s.Length);
            return $"{s.Substring(0, 6)}…{s.Substring(s.Length - 4)}";
        }

        private static string? Preview(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        // GET /api/esu/facebook/debug/me?businessId=...
        [HttpGet("me")]
        public async Task<IActionResult> GetMe([FromQuery] Guid businessId, CancellationToken ct)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            var me = await _graph.GetAsync<dynamic>(businessId, "me", new Dictionary<string, string?>
            {
                ["fields"] = "id,name"
            }, ct);

            return Ok(me);
        }
    }
}
