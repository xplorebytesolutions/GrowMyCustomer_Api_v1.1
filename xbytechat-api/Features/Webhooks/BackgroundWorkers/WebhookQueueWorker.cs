using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Webhooks.Services;

public class WebhookQueueWorker : BackgroundService
{
    private readonly IWebhookQueueService _queueService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookQueueWorker> _logger;

    public WebhookQueueWorker(
        IWebhookQueueService queueService,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookQueueWorker> logger)
    {
        _queueService = queueService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Webhook Queue Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Dequeue and clone once at the edge
                var payload = await _queueService.DequeueAsync(stoppingToken);
                var cloned = payload.Clone(); // keep independent of pooled reader
                var rawJson = cloned.GetRawText(); // 👈 capture raw JSON up-front

                using var scope = _scopeFactory.CreateScope();

                var dispatcher = scope.ServiceProvider.GetRequiredService<IWhatsAppWebhookDispatcher>();
                var failureLogger = scope.ServiceProvider.GetRequiredService<IFailedWebhookLogService>();

                try
                {
                    await dispatcher.DispatchAsync(cloned);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error while dispatching webhook payload.");
                    // Persist the full raw JSON so we can replay/debug later
                    var fallback = new FailedWebhookLogDto
                    {
                        SourceModule = "WebhookQueueWorker",
                        FailureType = "DispatchError",
                        ErrorMessage = ex.Message,
                        RawJson = string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson,
                        CreatedAt = DateTime.UtcNow
                    };
                    try
                    {
                        await failureLogger.LogFailureAsync(fallback);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "⚠️ Failed to write to FailedWebhookLogs.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 Graceful shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Queue loop error (will retry shortly).");
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch { /* ignore */ }
            }
        }

        _logger.LogInformation("🛑 Webhook Queue Worker stopped.");
    }
}


