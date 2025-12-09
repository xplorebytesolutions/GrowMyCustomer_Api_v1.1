using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Features.CampaignModule.Workers
{
    public sealed class OutboxReaperWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<OutboxReaperWorker> _log;

        // How often we sweep
        private static readonly TimeSpan SweepEvery = TimeSpan.FromSeconds(30);

        // Keep in sync with your sender logic / DEFAULT_MAX_ATTEMPTS
        private const int MAX_ATTEMPTS = 3;

        public OutboxReaperWorker(IServiceProvider sp, ILogger<OutboxReaperWorker> log)
        {
            _sp = sp;
            _log = log;
        }

        // OutboxReaperWorker.cs
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = DateTime.UtcNow;

                    // ---- Recover stale InFlight jobs (no Random in the expression) ----
                    var jitterSeconds = 2 + (int)((ulong)DateTime.UtcNow.Ticks % 3); // 2..4s
                    var backoffAt = now.AddSeconds(jitterSeconds);

                    var recovered = await db.OutboundMessageJobs
                        .Where(j => j.Status == "InFlight" &&
                                    j.NextAttemptAt != null &&
                                    j.NextAttemptAt < now)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(j => j.Status, _ => "Pending")
                            .SetProperty(j => j.NextAttemptAt, _ => backoffAt)
                            .SetProperty(j => j.LastError, _ => "Recovered from stale in-flight"),
                            stoppingToken);

                    // ---- Kill over-retried jobs ----
                    const int MAX_ATTEMPTS = 3;
                    var killed = await db.OutboundMessageJobs
                        .Where(j => (j.Status == "Pending" || j.Status == "Failed" || j.Status == "InFlight") &&
                                    j.Attempt >= MAX_ATTEMPTS)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(j => j.Status, _ => "Dead")
                            .SetProperty(j => j.NextAttemptAt, _ => (DateTime?)null)
                            .SetProperty(j => j.LastError, _ => "Max attempts exceeded"),
                            stoppingToken);

                    if (recovered > 0 || killed > 0)
                        _log.LogInformation("[OutboxReaper] recovered={Recovered} killed={Killed}", recovered, killed);
                }
                catch (TaskCanceledException) { /* shutdown */ }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[OutboxReaper] sweep failed");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (TaskCanceledException) { /* shutdown */ }
            }
        }

    }
}
