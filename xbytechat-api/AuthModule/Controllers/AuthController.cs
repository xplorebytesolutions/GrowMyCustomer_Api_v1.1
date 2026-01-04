using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using xbytechat.api.AuthModule.DTOs;
using xbytechat.api.AuthModule.Services;
using xbytechat.api.Features.AccessControl.Services;
using xbytechat.api.Features.BusinessModule.DTOs;

namespace xbytechat.api.AuthModule.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAccessControlService _accessControlService;

        public AuthController(IAuthService authService, IAccessControlService accessControlService)
        {
            _authService = authService;
            _accessControlService = accessControlService;
        }

        // ✅ Login → return { token } (NO cookies)
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Token))
                return Unauthorized(new { success = false, message = result.Message });

            return Ok(new { token = result.Token });
        }

        // (Optional) Refresh token endpoint if you still issue refresh tokens.
        // Returns tokens in body (NO cookies).
        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);
            if (!result.Success) return Unauthorized(new { success = false, message = result.Message });

            dynamic data = result.Data!;
            return Ok(new
            {
                accessToken = data.accessToken,
                refreshToken = data.refreshToken
            });
        }

        // ✅ Signup
        [HttpPost("business-user-signup")]
        public async Task<IActionResult> Signup([FromBody] SignupBusinessDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    success = false,
                    message = "❌ Validation failed.",
                    errors
                });
            }

            var result = await _authService.SignupAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ✅ Logout (stateless JWT): nothing server-side to do
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout() => Ok(new { success = true, message = "Logged out" });

        // ✅ (Optional) lightweight session echo from claims (works with Bearer)
        [Authorize]
        [HttpGet("session")]
        public IActionResult GetSession()
        {
            var user = HttpContext.User;
            if (user?.Identity is not { IsAuthenticated: true }) return BadRequest("Invalid session");

            var email = user.FindFirst(ClaimTypes.Email)?.Value ?? "unknown";
            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? "unknown";
            var plan = user.FindFirst("plan")?.Value ?? "basic";
            var biz = user.FindFirst("businessId")?.Value;

            return Ok(new { isAuthenticated = true, role, email, plan, businessId = biz });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var user = HttpContext.User;

            var userId =
                user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                user.FindFirst("uid")?.Value ??
                user.FindFirst("id")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { ok = false, message = "Missing user id in token." });
            }

            var email =
                user.FindFirst(ClaimTypes.Email)?.Value ??
                user.FindFirst(JwtRegisteredClaimNames.Email)?.Value ??
                user.FindFirst("email")?.Value;

            var role =
                user.FindFirst(ClaimTypes.Role)?.Value ??
                user.FindFirst("role")?.Value ?? "business";

            var businessId =
                user.FindFirst("businessId")?.Value ??
                user.FindFirst("BusinessId")?.Value ??
                user.FindFirst("business_id")?.Value;

            return Ok(new
            {
                ok = true,
                user = new
                {
                    id = userId,
                    email,
                    role
                },
                businessId,
                hasAllAccess = role.Equals("admin", StringComparison.OrdinalIgnoreCase)
                            || role.Equals("superadmin", StringComparison.OrdinalIgnoreCase)
                            || role.Equals("partner", StringComparison.OrdinalIgnoreCase)
                            || role.Equals("reseller", StringComparison.OrdinalIgnoreCase)
            });
        }

        // -----------------------------------------------------------
        // ✅ Main auth context: /api/auth/context
        // IMPORTANT: Read permissions/features FROM JWT CLAIMS (source of truth)
        // -----------------------------------------------------------
        [Authorize]
        [HttpGet("context")]
        public IActionResult GetContext()
        {
            var principal = HttpContext.User;

            // --- User id (mandatory) ---
            var userIdStr =
                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                principal.FindFirst("uid")?.Value ??
                principal.FindFirst("id")?.Value;

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new
                {
                    ok = false,
                    isAuthenticated = false,
                    message = "Invalid or missing user id claim."
                });
            }

            // --- Email ---
            var email =
                principal.FindFirst(ClaimTypes.Email)?.Value ??
                principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ??
                principal.FindFirst("email")?.Value;

            // --- User display name ---
            var userName =
                principal.FindFirst("name")?.Value ??
                principal.FindFirst(ClaimTypes.Name)?.Value ??
                principal.FindFirst("fullName")?.Value ??
                principal.FindFirst("fullname")?.Value;

            // --- Role ---
            var role =
                principal.FindFirst(ClaimTypes.Role)?.Value ??
                principal.FindFirst("role")?.Value ??
                "business";

            // --- BusinessId (optional) ---
            var businessIdClaim =
                principal.FindFirst("businessId")?.Value ??
                principal.FindFirst("BusinessId")?.Value ??
                principal.FindFirst("business_id")?.Value;

            Guid? businessId = null;
            if (Guid.TryParse(businessIdClaim, out var bizGuid))
                businessId = bizGuid;

            // --- PlanId (optional) ---
            var planIdClaim =
                principal.FindFirst("plan_id")?.Value ??
                principal.FindFirst("planId")?.Value;

            Guid? planId = null;
            if (Guid.TryParse(planIdClaim, out var planGuid))
                planId = planGuid;

            // --- Status ---
            var status =
                principal.FindFirst("status")?.Value ??
                principal.FindFirst("biz_status")?.Value ??
                principal.FindFirst("businessStatus")?.Value ??
                principal.FindFirst("bizStatus")?.Value ??
                "active";

            // --- Business name ---
            var companyName =
                principal.FindFirst("businessName")?.Value ??
                principal.FindFirst("companyName")?.Value ??
                principal.FindFirst("bizName")?.Value;

            // --- All-access roles ---
            var hasAllAccess =
                role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("partner", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("reseller", StringComparison.OrdinalIgnoreCase);

            // ✅ Permissions from JWT claim(s), not DB recompute
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in principal.Claims.Where(x => x.Type == "permissions"))
            {
                if (string.IsNullOrWhiteSpace(c.Value)) continue;

                foreach (var p in c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var code = p.Trim();
                    if (!string.IsNullOrWhiteSpace(code))
                        permissions.Add(code);
                }
            }

            // Optional: alternate claim name fallback (harmless)
            var permsAlt = principal.FindFirst("permission")?.Value;
            if (!string.IsNullOrWhiteSpace(permsAlt))
            {
                foreach (var p in permsAlt.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var code = p.Trim();
                    if (!string.IsNullOrWhiteSpace(code))
                        permissions.Add(code);
                }
            }

            var permsList = permissions.OrderBy(x => x).ToList();

            // ✅ Prefer features claim if present; else fallback to permissions
            var featuresSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in principal.Claims.Where(x => x.Type == "features"))
            {
                if (string.IsNullOrWhiteSpace(c.Value)) continue;

                foreach (var f in c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var key = f.Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                        featuresSet.Add(key);
                }
            }

            var features = featuresSet.Count > 0 ? featuresSet.OrderBy(x => x).ToList() : permsList;

            return Ok(new
            {
                ok = true,
                isAuthenticated = true,
                user = new
                {
                    id = userId,
                    email,
                    role,
                    name = userName,
                    fullName = userName,
                    displayName = userName
                },
                business = businessId.HasValue
                    ? new
                    {
                        id = businessId.Value,
                        businessId = businessId.Value,
                        businessName = companyName,
                        companyName = companyName,
                        planId,
                        status
                    }
                    : null,
                businessId,
                role,
                status,
                hasAllAccess,
                permissions = permsList,
                features,
                planId
            });
        }


        // ----------------- helpers -----------------

        private static List<string> ReadCsvClaim(ClaimsPrincipal principal, string claimType)
        {
            var raw = principal.FindFirst(claimType)?.Value;
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

            // Make it stable: trim + distinct (case-insensitive)
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var v = part.Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    set.Add(v);
            }

            return set.OrderBy(x => x).ToList();
        }
    }
}
