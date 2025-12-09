using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace xbytechat.api.Middlewares
{
    public class JwtErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context); // Proceed to next middleware
            }
            catch (SecurityTokenExpiredException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    success = false,
                    message = "❌ Token expired. Please login again."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (SecurityTokenException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    success = false,
                    message = $"❌ Token invalid: {ex.Message}"
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception)
            {
                // Pass unhandled exceptions to global exception middleware
                throw;
            }
        }
    }

    // Extension method for clean registration
    public static class JwtErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtErrorHandlingMiddleware>();
        }
    }
}
