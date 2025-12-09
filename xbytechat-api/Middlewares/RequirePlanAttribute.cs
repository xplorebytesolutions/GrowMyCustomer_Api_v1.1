using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace xbytechat.api.Middlewares
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class RequirePlanAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedPlans;

        public RequirePlanAttribute(params string[] allowedPlans)
        {
            _allowedPlans = allowedPlans;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var plan = context.HttpContext.User.FindFirst("plan")?.Value?.ToLowerInvariant();
            if (string.IsNullOrEmpty(plan) || !_allowedPlans.Any(p => p.ToLowerInvariant() == plan))
            {
                context.Result = new ForbidResult(); // 403 Forbidden
            }
        }
    }
}
