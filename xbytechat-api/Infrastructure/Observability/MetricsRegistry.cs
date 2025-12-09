using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;

namespace xbytechat.api.Infrastructure.Observability
{
    /// <summary>
    /// Central metrics facade with a hard OFF switch.
    /// When Observability:Metrics:Enabled=false, all calls no-op.
    /// </summary>
    public static class MetricsRegistry
    {
        private static bool _enabled;
        private static Meter? _meter;
        private static long _queueDepth;

        // Proxies let existing call sites stay unchanged.
        public static readonly CounterProxy MessagesSent = new();
        public static readonly CounterProxy MessagesFailed = new();
        public static readonly CounterProxy RateLimited429s = new();
        public static readonly HistProxy SendLatencyMs = new();

        /// <summary>Call once at startup from Program.cs.</summary>
        public static void Configure(IConfiguration cfg)
        {
            _enabled = cfg.GetValue<bool>("Observability:Metrics:Enabled", false);

            if (!_enabled)
            {
                _meter = null; // proxies remain unbound => no-ops
                return;
            }

            _meter = new Meter("xbytechat.api", "1.0.0");

            // Bind proxies to actual instruments
            MessagesSent.Bind(_meter.CreateCounter<long>("campaign_messages_sent", unit: "msg",
                description: "Accepted by provider"));
            MessagesFailed.Bind(_meter.CreateCounter<long>("campaign_messages_failed", unit: "msg",
                description: "Failed sends"));
            RateLimited429s.Bind(_meter.CreateCounter<long>("campaign_http_429", unit: "hit",
                description: "HTTP 429 responses"));
            SendLatencyMs.Bind(_meter.CreateHistogram<double>("campaign_send_latency_ms", unit: "ms",
                description: "Provider send latency"));

            _meter.CreateObservableGauge("campaign_queue_depth",
                () => new Measurement<long>(_queueDepth),
                unit: "jobs",
                description: "Outbound campaign jobs waiting in this process");
        }

        public static void ReportQueueDepth(long depth)
        {
            if (_enabled) _queueDepth = depth;
        }

        // ---------- Proxies (no-ops when not bound / disabled) ----------

        public sealed class CounterProxy
        {
            private Counter<long>? _inner;
            internal void Bind(Counter<long> inner) => _inner = inner;
            public void Add(long value) { if (_enabled) _inner?.Add(value); }
        }

        public sealed class HistProxy
        {
            private Histogram<double>? _inner;
            internal void Bind(Histogram<double> inner) => _inner = inner;
            public void Record(double value) { if (_enabled) _inner?.Record(value); }
        }
    }
}
