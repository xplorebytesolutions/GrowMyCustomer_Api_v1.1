using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xbytechat.api; // AppDbContext

namespace xbytechat.api.WhatsAppSettings.Services
{
    /// <summary>
    /// Periodically refreshes WhatsApp template catalogs for all businesses
    /// that have an active WhatsAppSettings row. Uses ITemplateSyncService
    /// which is TTL-aware to avoid unnecessary work.
    /// </summary>
    public sealed class TemplateSyncWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<TemplateSyncWorker> _log;
        private readonly TimeSpan _interval;
        private readonly int _maxParallel;
        private readonly int _jitterSeconds;

        public TemplateSyncWorker(IServiceProvider sp, ILogger<TemplateSyncWorker> log, IConfiguration cfg)
        {
            _sp = sp;
            _log = log;

            // Defaults chosen to be light on the system.
            // appsettings.json example:
            // "WhatsApp": {
            //   "Templates": {
            //     "SyncIntervalMinutes": 360,
            //     "MaxParallel": 1,
            //     "JitterSeconds": 60
            //   }
            // }
            var minutes = Math.Max(15, cfg.GetValue<int?>("WhatsApp:Templates:SyncIntervalMinutes") ?? 360);
            _interval = TimeSpan.FromMinutes(minutes);

            _maxParallel = Math.Clamp(cfg.GetValue<int?>("WhatsApp:Templates:MaxParallel") ?? 1, 1, 8);
            _jitterSeconds = Math.Clamp(cfg.GetValue<int?>("WhatsApp:Templates:JitterSeconds") ?? 60, 0, 300);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // small delay after startup
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            var rnd = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                var sweepStart = DateTime.UtcNow;
                var sw = Stopwatch.StartNew();
                int processed = 0;

                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Get active business IDs once per sweep
                    var bizIds = await db.WhatsAppSettings
                        .AsNoTracking()
                        .Where(s => s.IsActive)
                        .Select(s => s.BusinessId)
                        .Distinct()
                        .ToListAsync(stoppingToken);

                    if (bizIds.Count == 0)
                    {
                        _log.LogInformation("TemplateSyncWorker: no active WhatsApp settings found.");
                    }
                    else
                    {
                        var sem = new SemaphoreSlim(_maxParallel);
                        var tasks = bizIds.Select(async biz =>
                        {
                            await sem.WaitAsync(stoppingToken);
                            try
                            {
                                // New scope per business to keep DbContexts short-lived
                                using var inner = _sp.CreateScope();
                                var sync = inner.ServiceProvider.GetRequiredService<ITemplateSyncService>();

                                // TTL-aware; manual button should call force:true instead
                                var res = await sync.SyncBusinessTemplatesAsync(biz, force: false, ct: stoppingToken);
                                Interlocked.Increment(ref processed);

                                _log.LogInformation(
                                    "TemplateSyncWorker: biz={Biz} added={A} updated={U} skipped={S} syncedAt={At}",
                                    biz, res.Added, res.Updated, res.Skipped, res.SyncedAt);
                            }
                            catch (OperationCanceledException) { /* shutting down */ }
                            catch (Exception exBiz)
                            {
                                _log.LogWarning(exBiz, "TemplateSyncWorker: sync failed for biz {Biz}", biz);
                            }
                            finally
                            {
                                sem.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    }
                }
                catch (OperationCanceledException) { /* shutting down */ }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "TemplateSyncWorker sweep failed");
                }
                finally
                {
                    sw.Stop();
                    _log.LogInformation("TemplateSyncWorker: sweep finished in {ElapsedMs} ms; processed={Processed}; next run ~{NextRun}",
                        sw.ElapsedMilliseconds, processed, sweepStart.Add(_interval));
                }

                // Add small jitter to avoid multiple instances syncing at the exact same time
                var jitter = _jitterSeconds > 0 ? TimeSpan.FromSeconds(rnd.Next(0, _jitterSeconds + 1)) : TimeSpan.Zero;
                var delay = _interval + jitter;

                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}


//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using xbytechat.api; // AppDbContext

//namespace xbytechat.api.WhatsAppSettings.Services
//{
//    /// <summary>
//    /// Periodically refreshes WhatsApp template catalogs for all businesses
//    /// that have an active WhatsAppSettings row. Uses your existing
//    /// ITemplateSyncService (TTL-aware) so this is safe to run frequently.
//    /// </summary>
//    public sealed class TemplateSyncWorker : BackgroundService
//    {
//        private readonly IServiceProvider _sp;
//        private readonly ILogger<TemplateSyncWorker> _log;
//        private readonly TimeSpan _interval;

//        public TemplateSyncWorker(IServiceProvider sp, ILogger<TemplateSyncWorker> log, IConfiguration cfg)
//        {
//            _sp = sp;
//            _log = log;

//            // Default: every 6 hours; override in appsettings:
//            // "WhatsApp": { "Templates": { "SyncIntervalMinutes": 360 } }
//            var minutes = Math.Max(15, cfg.GetValue<int?>("WhatsApp:Templates:SyncIntervalMinutes") ?? 360);
//            _interval = TimeSpan.FromMinutes(minutes);
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            // small delay after startup
//            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    using var scope = _sp.CreateScope();
//                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//                    var sync = scope.ServiceProvider.GetRequiredService<ITemplateSyncService>();

//                    var bizIds = await db.WhatsAppSettings
//                        .AsNoTracking()
//                        .Where(s => s.IsActive)
//                        .Select(s => s.BusinessId)
//                        .Distinct()
//                        .ToListAsync(stoppingToken);

//                    foreach (var biz in bizIds)
//                    {
//                        try
//                        {
//                            var res = await sync.SyncBusinessTemplatesAsync(biz, force: false, ct: stoppingToken);
//                            _log.LogInformation("TemplateSyncWorker: biz={Biz} added={A} updated={U} syncedAt={At}",
//                                biz, res.Added, res.Updated, res.SyncedAt);
//                        }
//                        catch (Exception exBiz)
//                        {
//                            _log.LogWarning(exBiz, "TemplateSyncWorker: sync failed for biz {Biz}", biz);
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _log.LogWarning(ex, "TemplateSyncWorker sweep failed");
//                }

//                await Task.Delay(_interval, stoppingToken);
//            }
//        }
//    }
//}
