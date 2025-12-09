using System;
using System.Security.Claims;

namespace xbytechat.api.Shared
{
    public static class ClaimsBusinessDetails
    {
        public static Guid GetBusinessId(this ClaimsPrincipal user)
        {
            var businessIdClaim = user.FindFirst("businessId")?.Value; // lowercase only!
            if (string.IsNullOrEmpty(businessIdClaim) || !Guid.TryParse(businessIdClaim, out var businessId))
                throw new UnauthorizedAccessException("Invalid or missing businessId in token.");
            return businessId;
        }

        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid or missing userId in token.");
            return userId;
        }
    }
}
