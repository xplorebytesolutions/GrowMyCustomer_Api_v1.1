using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Helpers;

namespace xbytechat.api.AuthModule.Controllers
{
    [ApiController]
    [Route("api/auth/test")]
    public class TestJwtController : ControllerBase
    {
        [Authorize]
        [HttpGet("get-logged-in")]
        public IActionResult GetLoggedInUserInfo()
        {
            var userId = UserContextHelper.GetUserId(User);
            var businessId = UserContextHelper.GetBusinessId(User);
            var role = UserContextHelper.GetRole(User);
            var plan = UserContextHelper.GetPlan(User);
            var companyName = UserContextHelper.GetCompanyName(User);

            return Ok(new
            {
                success = true,
                message = "🔐 JWT is valid. Here's your decoded info:",
                data = new
                {
                    userId,
                    businessId,
                    role,
                    plan,
                    companyName
                }
            });
        }

        [HttpGet("get-current-user")]
        public IActionResult GetCurrentUser()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { success = false, message = "❌ Not authenticated" });
            }

            var userId = User.FindFirst("sub")?.Value;
            var email = User.FindFirst("email")?.Value;
            var role = User.FindFirst("role")?.Value;
            var businessId = User.FindFirst("businessId")?.Value;
            var plan = User.FindFirst("plan")?.Value;
            var permissions = User.FindFirst("permissions")?.Value;

            return Ok(new
            {
                success = true,
                message = "✅ Token is valid",
                user = new
                {
                    userId,
                    email,
                    role,
                    businessId,
                    plan,
                    permissions
                }
            });
        }
    }
}

