using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace xbytechat.api.Shared
{
    public static class ClaimsBusinessDetails
    {
        // ----- Existing behavior (strict) -----
        public static Guid GetBusinessId(this ClaimsPrincipal user)
        {
            var businessIdClaim =
                user.FindFirst("businessId")?.Value ??
                user.FindFirst("BusinessId")?.Value;

            if (string.IsNullOrEmpty(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
                throw new UnauthorizedAccessException("Invalid or missing businessId in token.");

            return businessId;
        }

        // ✅ NEW: Non-throwing claim read
        public static Guid? TryGetBusinessIdFromClaims(this ClaimsPrincipal user)
        {
            var businessIdClaim =
                user.FindFirst("businessId")?.Value ??
                user.FindFirst("BusinessId")?.Value;

            if (string.IsNullOrWhiteSpace(businessIdClaim)) return null;
            if (!Guid.TryParse(businessIdClaim, out var businessId)) return null;
            return businessId;
        }

        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid or missing userId in token.");
            return userId;
        }

        // ✅ NEW: Determine if caller is platform-role
        public static bool IsPlatformRole(this ClaimsPrincipal user)
        {
            var role =
                user.FindFirst(ClaimTypes.Role)?.Value ??
                user.FindFirst("role")?.Value ??
                user.FindFirst("roles")?.Value ??
                "";

            role = role.Trim().ToLowerInvariant();
            return role is "admin" or "superadmin" or "partner" or "reseller";
        }

        // ✅ NEW: Claim-first, fallback ONLY for platform roles
        // Fallback sources:
        // - Query: ?bizId=...
        // - Header: X-Business-Id: ...
        public static Guid ResolveBusinessId(this ClaimsPrincipal user, HttpContext http)
        {
            // 1) Prefer claim always
            var claimBiz = user.TryGetBusinessIdFromClaims();
            if (claimBiz.HasValue && claimBiz.Value != Guid.Empty)
                return claimBiz.Value;

            // 2) Only platform roles can fallback
            if (!user.IsPlatformRole())
                throw new UnauthorizedAccessException("Invalid or missing businessId in token.");

            // 3) Try query string (SignalR)
            var qs = http?.Request?.Query["bizId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(qs) && Guid.TryParse(qs, out var qBiz) && qBiz != Guid.Empty)
                return qBiz;

            // 4) Try header (APIs)
            var hdr = http?.Request?.Headers["X-Business-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(hdr) && Guid.TryParse(hdr, out var hBiz) && hBiz != Guid.Empty)
                return hBiz;

            // 5) Still missing => tell UI to select business
            throw new UnauthorizedAccessException("Business context required. Please select a business.");
        }
    }
}


//using System;
//using System.Security.Claims;

//namespace xbytechat.api.Shared
//{
//    public static class ClaimsBusinessDetails
//    {
//        public static Guid GetBusinessId(this ClaimsPrincipal user)
//        {
//            var businessIdClaim = user.FindFirst("businessId")?.Value; // lowercase only!
//            if (string.IsNullOrEmpty(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
//                throw new UnauthorizedAccessException("Invalid or missing businessId in token.");
//            return businessId;
//        }

//        public static Guid GetUserId(this ClaimsPrincipal user)
//        {
//            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
//                throw new UnauthorizedAccessException("Invalid or missing userId in token.");
//            return userId;
//        }
//    }
//}
