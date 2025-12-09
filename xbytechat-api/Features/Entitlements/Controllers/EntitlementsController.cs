#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Features.Entitlements.Services;
using xbytechat.api.Helpers; // ✅ Use shared helpers for claims

namespace xbytechat.api.Features.Entitlements.Controllers
{
    [ApiController]
    [Route("api/entitlements")]
    [Authorize]
    public sealed class EntitlementsController : ControllerBase
    {
        private readonly IQuotaService _quota;

        // Roles are stored lower-case in JWT (JwtTokenService),
        // so we treat "admin" and "superadmin" as global admins.
        private const string AdminRoleAdmin = "admin";
        private const string AdminRoleSuperAdmin = "superadmin";

        public EntitlementsController(IQuotaService quota)
        {
            _quota = quota;
        }

        // Helpers
        private Guid? TryGetCallerBusinessId()
        {
            // Centralized logic: reads "businessId" claim.
            var id = UserContextHelper.GetBusinessId(User);
            return id == Guid.Empty ? (Guid?)null : id;
        }

        private bool IsAdmin()
        {
            return User.IsInRole(AdminRoleAdmin) || User.IsInRole(AdminRoleSuperAdmin);
        }

        private bool IsAuthorizedFor(Guid targetBusinessId)
        {
            if (IsAdmin()) return true;

            var callerBiz = TryGetCallerBusinessId();
            return callerBiz.HasValue && callerBiz.Value == targetBusinessId;
        }

        // GET /api/entitlements/{businessId}
        [HttpGet("{businessId:guid}")]
        public async Task<ActionResult<EntitlementsSnapshotDto>> GetSnapshot(
            Guid businessId,
            CancellationToken ct)
        {
            if (!IsAuthorizedFor(businessId))
                return Forbid();

            var dto = await _quota.GetSnapshotAsync(businessId, ct);
            return Ok(dto);
        }

        // POST /api/entitlements/{businessId}/check
        [HttpPost("{businessId:guid}/check")]
        public async Task<ActionResult<EntitlementResultDto>> Check(
            Guid businessId,
            [FromBody] EntitlementCheckDto? req,
            CancellationToken ct)
        {
            if (!IsAuthorizedFor(businessId))
                return Forbid();

            if (req is null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(req.QuotaKey))
                return BadRequest("QuotaKey required.");

            var amount = Math.Max(1, req.Amount);

            var result = req.ConsumeOnSuccess
                ? await _quota.CheckAndConsumeAsync(businessId, req.QuotaKey, amount, ct)
                : await _quota.CheckAsync(businessId, req.QuotaKey, amount, ct);

            if (!result.Allowed)
                // 429 payload shape is already what your axios interceptor expects:
                // { allowed:false, quotaKey, limit, remaining, message }
                return StatusCode(429, result);

            return Ok(result);
        }
    }
}
