using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Security.Claims;

namespace xbytechat.api.AuthModule.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(
            string userId,
            string role,
            string userName,
            string email,
            string status,
            string businessId,
            string companyName,
                       List<string> permissions,
            string planId,
              List<string>? features = null,
            bool hasAllAccess = false
        );
        string GenerateToken(IEnumerable<Claim> claims);
        TokenValidationParameters GetValidationParameters(); // ✅ For Middleware validation
    }
}

