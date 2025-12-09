using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Security.Claims;

namespace xbytechat.api.Middleware.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _requiredPermission;

        public RequirePermissionAttribute(string requiredPermission)
        {
            _requiredPermission = requiredPermission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var permissionsClaim = user.Claims.FirstOrDefault(c => c.Type == "permissions")?.Value;

            if (permissionsClaim == null || !permissionsClaim.Split(',').Contains(_requiredPermission))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}

