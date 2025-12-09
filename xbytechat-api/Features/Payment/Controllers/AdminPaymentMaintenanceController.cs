#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Payment.Services;

namespace xbytechat.api.Features.Payment.Controllers
{
    /// <summary>
    /// Admin-only endpoints for running payment/subscription maintenance tasks.
    /// In production, prefer a scheduled job instead of manual calling.
    /// </summary>
    [ApiController]
    [Route("api/admin/payment/maintenance")]
    [Authorize(Roles = "SuperAdmin")] // adjust to your actual admin role
    public sealed class AdminPaymentMaintenanceController : ControllerBase
    {
        private readonly SubscriptionLifecycleService _lifecycle;

        public AdminPaymentMaintenanceController(SubscriptionLifecycleService lifecycle)
        {
            _lifecycle = lifecycle;
        }

        /// <summary>
        /// Runs subscription lifecycle sync manually.
        /// </summary>
        [HttpPost("run-lifecycle")]
        public async Task<IActionResult> RunLifecycle(CancellationToken ct)
        {
            await _lifecycle.RunAsync(ct);
            return Ok(new { ok = true });
        }
    }
}
