using Microsoft.EntityFrameworkCore;
using xbytechat.api;

namespace xbytechat.api.Features.CampaignTracking.Worker
{
    public sealed class ClickLogWorker : BackgroundService
    {
        private readonly ILogger<ClickLogWorker> _log;
        private readonly IClickEventQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        public ClickLogWorker(
            ILogger<ClickLogWorker> log,
            IClickEventQueue queue,
            IServiceScopeFactory scopeFactory)
        {
            _log = log;
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("ClickLogWorker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                List<ClickEvent> batch;
                try
                {
                    batch = await _queue.ReadBatchAsync(200, TimeSpan.FromSeconds(1), stoppingToken);
                    if (batch.Count == 0) continue;

                    // quick visibility: confirm we are ingesting call/whatsapp/web events
                    var byType = batch.GroupBy(e => e.ClickType ?? "web")
                                      .Select(g => $"{g.Key}:{g.Count()}")
                                      .ToArray();
                    _log.LogInformation("WORKER processing {Count} events [{Kinds}]",
                        batch.Count, string.Join(", ", byType));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Queue read failed; retrying");
                    try { await Task.Delay(500, stoppingToken); } catch { /* ignore */ }
                    continue;
                }

                // nothing to do
                if (batch.Count == 0) continue;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Prefetch CampaignId for this batch (we only need CampaignId for the aggregates)
                    var sendIds = batch.Select(b => b.CampaignSendLogId).Distinct().ToList();
                    if (sendIds.Count == 0) continue;

                    var sendMap = await db.CampaignSendLogs
                        .Where(s => sendIds.Contains(s.Id))
                        .Select(s => new { s.Id, s.CampaignId })
                        .ToDictionaryAsync(s => s.Id, s => s.CampaignId, stoppingToken);

                    // Prepare aggregate groups: (CampaignId, Day, ButtonIndex) -> count
                    var groups = batch
                        .Select(e =>
                        {
                            sendMap.TryGetValue(e.CampaignSendLogId, out var campaignId);
                            return new { CampaignId = campaignId, Day = e.ClickedAtUtc.Date, e.ButtonIndex };
                        })
                        .Where(x => x.CampaignId != Guid.Empty)
                        .GroupBy(x => new { x.CampaignId, x.Day, x.ButtonIndex })
                        .Select(g => new { g.Key.CampaignId, g.Key.Day, g.Key.ButtonIndex, Count = g.Count() })
                        .ToList();

                    if (groups.Count == 0) continue;

                    foreach (var g in groups)
                    {
                        await db.Database.ExecuteSqlRawAsync(@"
                    insert into ""CampaignClickDailyAgg"" (""CampaignId"", ""Day"", ""ButtonIndex"", ""Clicks"")
                    values ({0}, {1}, {2}, {3})
                    on conflict (""CampaignId"", ""Day"", ""ButtonIndex"")
                    do update set ""Clicks"" = ""CampaignClickDailyAgg"".""Clicks"" + {3};",
                            g.CampaignId, g.Day, g.ButtonIndex, g.Count);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Aggregate update failed; skipped this batch.");
                }
            }

            _log.LogInformation("ClickLogWorker stopped");
        }
    }
}


//using Microsoft.EntityFrameworkCore;
//using xbytechat.api;

//namespace xbytechat.api.Features.CampaignTracking.Worker
//{
//    public sealed class ClickLogWorker : BackgroundService
//    {
//        private readonly ILogger<ClickLogWorker> _log;
//        private readonly IClickEventQueue _queue;
//        private readonly IServiceScopeFactory _scopeFactory;

//        public ClickLogWorker(
//            ILogger<ClickLogWorker> log,
//            IClickEventQueue queue,
//            IServiceScopeFactory scopeFactory)
//        {
//            _log = log;
//            _queue = queue;
//            _scopeFactory = scopeFactory;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _log.LogInformation("ClickLogWorker started");

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                List<ClickEvent> batch;
//                try
//                {
//                    batch = await _queue.ReadBatchAsync(200, TimeSpan.FromSeconds(1), stoppingToken);
//                    if (batch.Count == 0) continue;
//                    _log.LogInformation("WORKER processing {Count} events (aggregates only)", batch.Count);
//                }
//                catch (OperationCanceledException) { break; }
//                catch (Exception ex)
//                {
//                    _log.LogError(ex, "Queue read failed; retrying");
//                    try { await Task.Delay(500, stoppingToken); } catch { }
//                    continue;
//                }

//                try
//                {
//                    using var scope = _scopeFactory.CreateScope();
//                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

//                    // Prefetch CampaignId for this batch
//                    var sendIds = batch.Select(b => b.CampaignSendLogId).Distinct().ToList();
//                    var sendMap = await db.CampaignSendLogs
//                        .Where(s => sendIds.Contains(s.Id))
//                        .Select(s => new { s.Id, s.CampaignId })
//                        .ToDictionaryAsync(s => s.Id, s => s.CampaignId, stoppingToken);

//                    // Build aggregate groups
//                    var groups = batch
//                        .Select(e =>
//                        {
//                            sendMap.TryGetValue(e.CampaignSendLogId, out var campaignId);
//                            return new { CampaignId = campaignId, Day = e.ClickedAtUtc.Date, e.ButtonIndex };
//                        })
//                        .Where(x => x.CampaignId != Guid.Empty)
//                        .GroupBy(x => new { x.CampaignId, x.Day, x.ButtonIndex })
//                        .Select(g => new { g.Key.CampaignId, g.Key.Day, g.Key.ButtonIndex, Count = g.Count() })
//                        .ToList();

//                    foreach (var g in groups)
//                    {
//                        await db.Database.ExecuteSqlRawAsync(@"
//insert into ""CampaignClickDailyAgg"" (""CampaignId"", ""Day"", ""ButtonIndex"", ""Clicks"")
//values ({0}, {1}, {2}, {3})
//on conflict (""CampaignId"", ""Day"", ""ButtonIndex"")
//do update set ""Clicks"" = ""CampaignClickDailyAgg"".""Clicks"" + {3};",
//                            g.CampaignId, g.Day, g.ButtonIndex, g.Count);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _log.LogError(ex, "Aggregate update failed; skipped.");
//                }
//            }

//            _log.LogInformation("ClickLogWorker stopped");
//        }
//    }
//}
