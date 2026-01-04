using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace xbytechat.api.AuthModule.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(IConfiguration config, ILogger<JwtTokenService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public string GenerateToken(
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
            bool hasAllAccess = false)
        {
            try
            {
                var permissionString = string.Join(",", permissions ?? new List<string>());
                var featuresString = string.Join(",", features ?? new List<string>());

                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId),
                    new Claim("id", userId),
                    new Claim(ClaimTypes.NameIdentifier, userId),

                    new Claim("email", email ?? ""),
                    new Claim("name", userName ?? ""),
                    new Claim("status", status ?? "unknown"),

                    // 🔐 Business id: add BOTH for compatibility
                    new Claim("BusinessId", businessId ?? ""), // <-- used by our helpers/controllers
                    new Claim("businessId", businessId ?? ""), // <-- keep for existing clients

                    new Claim("companyName", companyName ?? ""),

                    // 🔖 Role (API + UI)
                    new Claim("role", role?.ToLowerInvariant() ?? "unknown"),
                    new Claim(ClaimTypes.Role, role?.ToLowerInvariant() ?? "unknown"),

                    // 🧩 Access
                    new Claim("permissions", permissionString),
                    new Claim("features", featuresString),
                    new Claim("hasAllAccess", hasAllAccess ? "true" : "false"),
                };

                // ✅ FIX: Only include plan_id claim when we actually want to expose it
                // This matches RefreshTokenAsync behavior (no empty plan_id claim).
                if (!string.IsNullOrWhiteSpace(planId))
                {
                    claims.Add(new Claim("plan_id", planId));
                }

                return GenerateToken(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating token for userId: {UserId}", userId);
                throw;
            }
        }

        public string GenerateToken(IEnumerable<Claim> claims)
        {
            try
            {
                var jwtSettings = _config.GetSection("JwtSettings");

                var secret = jwtSettings["SecretKey"];
                if (string.IsNullOrEmpty(secret))
                {
                    _logger.LogWarning("⚠️ JWT SecretKey is missing from configuration.");
                    throw new Exception("JWT SecretKey is not configured.");
                }

                var expiry = jwtSettings["ExpiryMinutes"];
                if (!int.TryParse(expiry, out var expiryMinutes))
                {
                    _logger.LogWarning("⚠️ JWT ExpiryMinutes is invalid or missing. Defaulting to 60 minutes.");
                    expiryMinutes = 60;
                }

                var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
                var creds = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

                var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);
                var unixExp = new DateTimeOffset(expires).ToUnixTimeSeconds();

                var finalClaims = claims.ToList();
                finalClaims.Add(new Claim(JwtRegisteredClaimNames.Exp, unixExp.ToString()));

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: finalClaims,
                    expires: expires,
                    signingCredentials: creds
                );

                _logger.LogInformation("✅ Token generated for: {Email}", finalClaims.FirstOrDefault(c => c.Type == "email")?.Value);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating JWT from claims.");
                throw;
            }
        }

        public TokenValidationParameters GetValidationParameters()
        {
            var jwtSettings = _config.GetSection("JwtSettings");

            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings["SecretKey"])
                ),
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = "role",
                NameClaimType = "name"
            };
        }
    }
}
