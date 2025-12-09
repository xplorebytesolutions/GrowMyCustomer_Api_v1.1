using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace xbytechat.api.Features.CampaignTracking.Logging
{
    public class CampaignLogFlushWorker : BackgroundService
    {
        private readonly ICampaignLogSink _sink;
        private readonly ILogger<CampaignLogFlushWorker> _log;
        private readonly IOptionsMonitor<BatchingOptions> _opts;

        public CampaignLogFlushWorker(ICampaignLogSink sink, ILogger<CampaignLogFlushWorker> log, IOptionsMonitor<BatchingOptions> opts)
        {
            _sink = sink; _log = log; _opts = opts;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await _sink.FlushAsync(stoppingToken); }
                catch (Exception ex) { _log.LogError(ex, "[CampaignLogFlushWorker] flush error"); }
                await Task.Delay(TimeSpan.FromMilliseconds(_opts.CurrentValue.CampaignLog.FlushEveryMs), stoppingToken);
            }
        }
    }
}
