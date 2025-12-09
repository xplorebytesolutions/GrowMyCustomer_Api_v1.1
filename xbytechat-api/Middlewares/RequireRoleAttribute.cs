using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace xbytechat.api.Middlewares
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class RequireRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public RequireRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var role = context.HttpContext.User.FindFirst("role")?.Value?.ToLowerInvariant();
            if (string.IsNullOrEmpty(role) || !_roles.Any(r => r.ToLowerInvariant() == role))
            {
                context.Result = new ForbidResult(); // 403 Forbidden
            }
        }
    }
}
