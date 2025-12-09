#nullable enable
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using xbytechat.api.Features.Payment.Services;
using xbytechat.api.Shared; // for User.GetBusinessId()

namespace xbytechat.api.Features.Payment.Filters
{
    /// <summary>
    /// Use [RequireActiveSubscription] on controllers/actions that should be accessible
    /// only when the business has an allowed subscription state.
    /// 
    /// Logic is delegated to IAccessGuard so rules stay centralized.
    /// </summary>
    public sealed class RequireActiveSubscriptionAttribute : TypeFilterAttribute
    {
        public RequireActiveSubscriptionAttribute()
            : base(typeof(RequireActiveSubscriptionFilter))
        {
        }

        private sealed class RequireActiveSubscriptionFilter : IAsyncActionFilter
        {
            private readonly IAccessGuard _accessGuard;

            public RequireActiveSubscriptionFilter(IAccessGuard accessGuard)
            {
                _accessGuard = accessGuard;
            }

            public async Task OnActionExecutionAsync(
     ActionExecutingContext context,
     ActionExecutionDelegate next)
            {
                var user = context.HttpContext.User;

                // Let [Authorize] handle unauthenticated cases.
                if (user?.Identity is null || !user.Identity.IsAuthenticated)
                {
                    await next();
                    return;
                }

                var businessId = user.GetBusinessId();

                var result = await _accessGuard.CheckAsync(businessId);

                if (!result.Allowed)
                {
                    context.Result = new ObjectResult(new
                    {
                        ok = false,
                        status = result.Status?.ToString(),
                        message = result.Message
                    })
                    {
                        StatusCode = 403
                    };

                    return;
                }

                await next();
            }

        }
    }
}
