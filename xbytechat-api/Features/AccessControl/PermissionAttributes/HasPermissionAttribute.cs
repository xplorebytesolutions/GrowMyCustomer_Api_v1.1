using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.Services;

namespace xbytechat.api.Features.AccessControl.PermissionAttributes
{
    /// <summary>
    /// Enforces a permission code on API endpoints.
    /// Priority:
    /// 1) hasAllAccess=true => allow (admin/superadmin/partner/reseller)
    /// 2) permissions claim contains code => allow
    /// 3) fallback (legacy): plan_id -> plan permissions cache
    /// </summary>
    public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public HasPermissionAttribute(string permissionCode) => _permissionCode = permissionCode;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new ForbidResult();
                return;
            }

            // ✅ 1) All-access shortcut (admins)
            var hasAllAccessClaim = user.FindFirst("hasAllAccess")?.Value;
            if (string.Equals(hasAllAccessClaim, "true", StringComparison.OrdinalIgnoreCase))
                return;

            // ✅ 2) Prefer the JWT permissions claim (fast, no DB hit)
            var permsClaim = user.FindFirst("permissions")?.Value;
            if (!string.IsNullOrWhiteSpace(permsClaim))
            {
                var perms = new HashSet<string>(
                    permsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase
                );

                if (perms.Contains(_permissionCode))
                    return;

                context.Result = new ForbidResult();
                return;
            }

            // 🧯 3) Legacy fallback: plan-only (kept for backward compatibility)
            var planIdClaim = user.FindFirst("plan_id")?.Value;
            if (string.IsNullOrWhiteSpace(planIdClaim) || !Guid.TryParse(planIdClaim, out var planId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var permissionService = context.HttpContext.RequestServices
                .GetRequiredService<IPermissionCacheService>();

            var planPermissions = await permissionService.GetPlanPermissionsAsync(planId);

            var hasPermission = planPermissions.Any(p =>
                string.Equals(p.Code, _permissionCode, StringComparison.OrdinalIgnoreCase));

            if (!hasPermission)
                context.Result = new ForbidResult();
        }
    }
}
