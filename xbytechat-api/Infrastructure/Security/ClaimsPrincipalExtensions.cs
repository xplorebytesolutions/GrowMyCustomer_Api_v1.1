using System.Security.Claims;

namespace xbytechat.api.Infrastructure.Security
{
    public static class ClaimsPrincipalExtensions
    {
        // Adjust these claim types if your project uses different names
        private const string RoleClaimType = ClaimTypes.Role;          // typical
        private const string AltRoleClaimType = "role";                // common JWT
        private const string BusinessIdClaimType = "BusinessId";       // your system likely has this

        public static string? GetRole(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(RoleClaimType)
                   ?? user.FindFirstValue(AltRoleClaimType);
        }

        public static Guid? GetBusinessId(this ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(BusinessIdClaimType);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        public static bool IsAdminLike(this ClaimsPrincipal user)
        {
            var role = (user.GetRole() ?? "").Trim();
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBusinessOwnerLike(this ClaimsPrincipal user)
        {
            var role = (user.GetRole() ?? "").Trim();

            // Keep this flexible for MVP; tighten later once your role codes are finalized
            return role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                || role.Equals("BusinessOwner", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Business_Admin", StringComparison.OrdinalIgnoreCase)
                || role.Equals("BusinessAdmin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
