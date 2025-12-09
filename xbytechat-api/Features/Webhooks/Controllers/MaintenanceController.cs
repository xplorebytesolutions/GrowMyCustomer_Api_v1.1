using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Webhooks.Models;
using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class MaintenanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMaintenanceService _maintenance;

        public MaintenanceController(AppDbContext context, IMaintenanceService maintenance)
        {
            _context = context;
            _maintenance = maintenance;
        }

        // ✅ Injected Test Log for Dev Testing
        [HttpPost("inject-test-log")]
        public async Task<IActionResult> InjectTestLog()
        {
            var testLog = new FailedWebhookLog
            {
                SourceModule = "WebhookQueueWorker",
                FailureType = "DispatchError",
                ErrorMessage = "🧪 Simulated webhook dispatch failure for testing.",
                RawJson = "{\"sample\":\"test_payload\",\"reason\":\"unit_test\"}",
                CreatedAt = DateTime.UtcNow
            };

            _context.FailedWebhookLogs.Add(testLog);
            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Injected test log successfully." });
        }

        // ✅ Manual Cleanup Trigger
        [HttpPost("cleanup-now")]
        public async Task<IActionResult> CleanupNow()
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var oldLogs = await _context.FailedWebhookLogs
                .Where(x => x.CreatedAt < cutoff)
                .ToListAsync();

            if (!oldLogs.Any())
                return Ok(new { message = "✅ No logs to delete." });

            _context.FailedWebhookLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"✅ Deleted {oldLogs.Count} old logs." });
        }

        // ✅ Count of all failed logs
        [HttpGet("failed/count")]
        public async Task<IActionResult> GetFailedCount()
        {
            var count = await _context.FailedWebhookLogs.CountAsync();
            return Ok(count);
        }

        // ✅ Cleanup Setting Status
             [HttpGet("settings")]
        public async Task<IActionResult> GetCleanupStatus()
        {
            var enabled = await _maintenance.IsAutoCleanupEnabledAsync();
            var lastRun = await _maintenance.GetLastCleanupTimeAsync();

            return Ok(new
            {
                enabled,
                lastCleanupAt = lastRun
            });
        }

        // ✅ Enable Auto Cleanup
        [HttpPost("enable-cleanup")]
        public async Task<IActionResult> EnableCleanup()
        {
            await _maintenance.EnableAutoCleanupAsync();
            return Ok(new { message = "✅ Auto-cleanup enabled." });
        }

        // ✅ Disable Auto Cleanup
        [HttpPost("disable-cleanup")]
        public async Task<IActionResult> DisableCleanup()
        {
            await _maintenance.DisableAutoCleanupAsync();
            return Ok(new { message = "✅ Auto-cleanup disabled." });
        }
    }
}
