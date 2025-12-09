#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace xbytechat.api.AuthModule.Controllers
{
    [ApiController]
    [Route("api/me")]
    public sealed class MeController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public IActionResult Get()
        {
            string? userId =
                User.FindFirst("uid")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value;

            string role =
                User.FindFirst("role")?.Value ??
                User.FindFirst(ClaimTypes.Role)?.Value ??
                "business";

            string? businessId =
                User.FindFirst("bid")?.Value ??
                User.FindFirst("BusinessId")?.Value ??
                User.FindFirst("business_id")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { ok = false, message = "Missing uid/sub claim." });
            }

            // Only enforce BusinessId for non-admins
            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(businessId))
            {
                return Unauthorized(new { ok = false, message = "Missing BusinessId claim." });
            }

            var name = User.Identity?.Name ?? "User";
            var permissions = new[] { "*" }; // replace later

            return Ok(new
            {
                ok = true,
                user = new { id = userId, name, role },
                businessId,          // can be null for admin
                hasAllAccess = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
            });
        }
    }
}


//#nullable enable
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using xbytechat.api.Shared;



//namespace xbytechat.api.AuthModule.Controllers
//{

//    [ApiController]
//    [Route("api/me")]
//    public sealed class MeController : ControllerBase
//    {
//        [HttpGet]
//        [Authorize] // requires valid JWT
//        public IActionResult Get()
//        {
//            try
//            {
//                var userId = User.GetUserId();        // throws if missing/invalid
//                var businessId = User.GetBusinessId(); // throws if missing/invalid

//                // Optional: expose a friendly name if you put it in the token
//                var name = User.Identity?.Name ?? "User";

//                // TODO: replace with your real permission source later
//                var permissions = new[] { "*" };

//                return Ok(new
//                {
//                    ok = true,
//                    user = new { id = userId, name },
//                    businessId,
//                    permissions
//                });
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                return Unauthorized(new { ok = false, message = ex.Message });
//            }
//        }
//    }
//}
