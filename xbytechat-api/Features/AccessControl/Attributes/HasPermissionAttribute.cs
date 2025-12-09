using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.Services;

namespace xbytechat.api.Features.AccessControl.Attributes
{
    public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public HasPermissionAttribute(string permissionCode) => _permissionCode = permissionCode;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            var planIdClaim = user.FindFirst("plan_id")?.Value;

            if (string.IsNullOrWhiteSpace(planIdClaim) || !Guid.TryParse(planIdClaim, out var planId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var permissionService = context.HttpContext.RequestServices
                .GetRequiredService<IPermissionCacheService>();

            var permissions = await permissionService.GetPlanPermissionsAsync(planId);

            var hasPermission = permissions.Any(p =>
                string.Equals(p.Code, _permissionCode, StringComparison.OrdinalIgnoreCase));

            if (!hasPermission)
                context.Result = new ForbidResult();
        }
    }
}


//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using Microsoft.Extensions.DependencyInjection;
//using System;
//using System.Linq;
//using xbytechat.api.Features.AccessControl.Services;

//namespace xbytechat.api.Features.AccessControl.Attributes
//{
//    public class HasPermissionAttribute : Attribute, IAuthorizationFilter
//    {
//        private readonly string _permissionCode;

//        public HasPermissionAttribute(string permissionCode)
//        {
//            _permissionCode = permissionCode;
//        }

//        public void OnAuthorization(AuthorizationFilterContext context)
//        {
//            var user = context.HttpContext.User;
//            var planIdClaim = user.FindFirst("plan_id")?.Value;

//            if (string.IsNullOrEmpty(planIdClaim))
//            {
//                context.Result = new ForbidResult();
//                return;
//            }

//            if (!Guid.TryParse(planIdClaim, out var planId))
//            {
//                context.Result = new ForbidResult();
//                return;
//            }

//            var permissionService = context.HttpContext.RequestServices
//                .GetRequiredService<IPermissionCacheService>();

//            // Get permissions for this plan from cache
//            var permissions = permissionService.GetPlanPermissionsAsync(planId).Result;

//            // Check if any permission matches the requested code
//            bool hasPermission = permissions.Any(p =>
//                string.Equals(p.Code, _permissionCode, StringComparison.OrdinalIgnoreCase));

//            if (!hasPermission)
//            {
//                context.Result = new ForbidResult();
//            }
//        }
//    }
//}
