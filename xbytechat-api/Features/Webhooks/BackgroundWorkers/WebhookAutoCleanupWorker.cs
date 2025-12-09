using xbytechat.api.Features.Webhooks.Services;

namespace xbytechat.api.Features.Webhooks.BackgroundWorkers
{
    public class WebhookAutoCleanupWorker : BackgroundService
    {
        private readonly ILogger<WebhookAutoCleanupWorker> _logger;
        private readonly IServiceProvider _services;

        public WebhookAutoCleanupWorker(IServiceProvider services, ILogger<WebhookAutoCleanupWorker> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                var maintenanceService = scope.ServiceProvider.GetRequiredService<IMaintenanceService>();

                if (await maintenanceService.IsAutoCleanupEnabledAsync())
                {
                    var count = await maintenanceService.RunCleanupAsync();
                    _logger.LogInformation($"🧹 Auto-cleaned {count} old webhook logs.");
                }

                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }
    }
}
