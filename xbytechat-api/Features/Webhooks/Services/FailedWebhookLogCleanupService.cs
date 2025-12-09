using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xbytechat.api;

namespace xbytechat.api.Features.Webhooks.Services
{
    /// <summary>
    /// Periodic cleanup of old failed webhook logs.
    /// Runs as a background service and exits cleanly on shutdown.
    /// </summary>
    public sealed class FailedWebhookLogCleanupService : BackgroundService
    {
        private readonly ILogger<FailedWebhookLogCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24); // daily run

        public FailedWebhookLogCleanupService(
            ILogger<FailedWebhookLogCleanupService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧹 FailedWebhookLogCleanupService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // If shutdown requested, bail out before touching DI / DbContext
                    stoppingToken.ThrowIfCancellationRequested();

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var cutoff = DateTime.UtcNow.AddDays(-7);

                    var oldLogs = await db.FailedWebhookLogs
                        .Where(x => x.CreatedAt < cutoff)
                        .ToListAsync(stoppingToken);

                    if (oldLogs.Count > 0)
                    {
                        db.FailedWebhookLogs.RemoveRange(oldLogs);
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation(
                            "🧹 Deleted {Count} old failed webhook logs.",
                            oldLogs.Count
                        );
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Normal shutdown – just break the loop
                    _logger.LogDebug(
                        "FailedWebhookLogCleanupService cancellation requested, exiting loop."
                    );
                    break;
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
                    // DI container / DbContext got disposed during shutdown; safe to ignore
                    _logger.LogDebug(
                        "AppDbContext/ServiceProvider disposed during shutdown in FailedWebhookLogCleanupService."
                    );
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to clean up old webhook logs.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug(
                        "FailedWebhookLogCleanupService delay cancelled, exiting loop."
                    );
                    break;
                }
            }

            _logger.LogInformation("🛑 FailedWebhookLogCleanupService stopped.");
        }
    }
}
