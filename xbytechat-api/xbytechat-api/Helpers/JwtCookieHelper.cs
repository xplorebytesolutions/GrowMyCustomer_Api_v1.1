// 📄 File: Helpers/JwtCookieHelper.cs
using Microsoft.AspNetCore.Http;
using System;

namespace xbytechat.api.Helpers
{
    public static class JwtCookieHelper
    {
        // ✅ Set Access Token (short-lived)
        public static void SetJwtCookie(HttpContext httpContext, string cookieName, string token, int expiryHours = 12)
        {
            if (httpContext == null || httpContext.Response.HasStarted)
            {
                Console.WriteLine($"⚠️ Cannot set JWT cookie '{cookieName}' — response already started.");
                return;
            }

            bool isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            httpContext.Response.Cookies.Append(cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                ///*Secure*/ = isProduction,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(expiryHours)
            }); ;
        }

        // ✅ Clear Access Token cookie
        public static void ClearJwtCookie(HttpContext httpContext, string cookieName)
        {
            if (httpContext == null || httpContext.Response.HasStarted)
            {
                Console.WriteLine($"⚠️ Cannot clear JWT cookie '{cookieName}' — response already started.");
                return;
            }

            httpContext.Response.Cookies.Append(cookieName, "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            });
        }

        // ✅ Set Refresh Token (long-lived)
        public static void SetRefreshTokenCookie(HttpContext httpContext, string cookieName, string refreshToken, int expiryDays = 30)
        {
            if (httpContext == null || httpContext.Response.HasStarted)
            {
                Console.WriteLine($"⚠️ Cannot set refresh cookie '{cookieName}' — response already started.");
                return;
            }

            bool isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            httpContext.Response.Cookies.Append(cookieName, refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = isProduction,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(expiryDays)
            });
        }

        // ✅ Clear Refresh Token cookie
        public static void ClearRefreshTokenCookie(HttpContext httpContext, string cookieName)
        {
            if (httpContext == null || httpContext.Response.HasStarted)
            {
                Console.WriteLine($"⚠️ Cannot clear refresh cookie '{cookieName}' — response already started.");
                return;
            }

            httpContext.Response.Cookies.Append(cookieName, "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            });
        }
    }
}
