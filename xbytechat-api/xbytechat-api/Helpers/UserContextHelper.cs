using System.Security.Claims;

namespace xbytechat.api.Helpers
{
    public static class UserContextHelper
    {
        /// <summary>
        /// Returns the logged-in user's unique ID from JWT.
        /// </summary>
        public static Guid GetUserId(ClaimsPrincipal user)
        {
            return Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Returns the business ID (tenant) from JWT claims.
        /// </summary>
        public static Guid GetBusinessId(ClaimsPrincipal user)
        {
            return Guid.TryParse(user.FindFirst("businessId")?.Value, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Returns the role of the logged-in user.
        /// </summary>
        public static string GetRole(ClaimsPrincipal user)
        {
            return user.FindFirst("role")?.Value ?? "";
        }

        /// <summary>
        /// Returns company name for UI display (optional).
        /// </summary>
        public static string GetCompanyName(ClaimsPrincipal user)
        {
            return user.FindFirst("companyName")?.Value ?? "";
        }

        /// <summary>
        /// Returns plan info if needed for plan-based access control.
        /// </summary>
        public static string GetPlan(ClaimsPrincipal user)
        {
            return user.FindFirst("plan")?.Value ?? "";
        }
    }
}
