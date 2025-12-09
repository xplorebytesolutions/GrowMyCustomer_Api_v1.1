using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CampaignModule.DTOs;


namespace xbytechat.api.Features.CampaignModule.Progress.Controllers
{
    // NOTE: route is plural to match your frontend: /api/campaigns/{id}/progress
    [ApiController]
    [Route("api/campaigns")]
    public class CampaignsProgressController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CampaignsProgressController(AppDbContext db) => _db = db;

        [HttpGet("{id:guid}/progress")]
        public async Task<ActionResult<CampaignProgressDto>> GetProgress([FromRoute] Guid id)
        {
            var businessId = GetBusinessIdOrThrow();

            // 1) Aggregate OutboundMessageJobs for this campaign
            var jobsQuery = _db.OutboundMessageJobs
                .AsNoTracking()
                .Where(j => j.BusinessId == businessId && j.CampaignId == id);

            var total = await jobsQuery.CountAsync();

            // group-by status safely (statuses are strings in your worker: Pending/InFlight/Sent/Failed)
            var grouped = await jobsQuery
                .GroupBy(j => j.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int pending = grouped.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0;
            int inFlight = grouped.FirstOrDefault(x => x.Status == "InFlight")?.Count ?? 0;
            int sent = grouped.FirstOrDefault(x => x.Status == "Sent")?.Count ?? 0;
            int failed = grouped.FirstOrDefault(x => x.Status == "Failed")?.Count ?? 0;

            // "Dead" = failed attempts that exceeded the worker retry policy.
            // Your worker uses DEFAULT_MAX_ATTEMPTS = 3; mark those as dead for visibility.
            const int MAX_ATTEMPTS = 3;
            int dead = await jobsQuery.Where(j => j.Status == "Failed" && j.Attempt >= MAX_ATTEMPTS).CountAsync();

            var completed = sent + failed; // treat both as terminal
            var pct = total > 0 ? (completed * 100.0) / total : 0.0;

            // 2) Percentiles from MessageLogs over the last hour (SentAt or CreatedAt → latency)
            // We’ll compute duration_ms = (COALESCE(SentAt, CreatedAt) - CreatedAt) in milliseconds
            // and use PostgreSQL percentile_cont via raw SQL. If DB isn’t PostgreSQL, you can replace with a fallback.

            double? p50 = null, p95 = null, p99 = null;

            try
            {
                var sql = @"
SELECT
  percentile_cont(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p50,
  percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p95,
  percentile_cont(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p99
FROM ""MessageLogs""
WHERE ""BusinessId"" = {0}
  AND ""CampaignId"" = {1}
  AND ""Source"" = 'campaign'
  AND ""CreatedAt"" >= NOW() - INTERVAL '1 hour';
";
                var row = await _db.Set<PercentileRow>()
                    .FromSqlRaw(sql, businessId, id)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (row != null)
                {
                    p50 = row.p50;
                    p95 = row.p95;
                    p99 = row.p99;
                }
            }
            catch
            {
                // Swallow percentile errors (e.g., not Postgres); UI shows "–" if null.
            }

            var dto = new CampaignProgressDto
            {
                CampaignId = id,
                TotalJobs = total,
                Pending = pending,
                InFlight = inFlight,
                Sent = sent,
                Failed = failed,
                Dead = dead,
                Completed = completed,
                CompletionPct = Math.Round(pct, 2),
                P50ms = p50,
                P95ms = p95,
                P99ms = p99,
                RetrievedAtUtc = DateTime.UtcNow
            };

            return Ok(dto);
        }

        private Guid GetBusinessIdOrThrow()
        {
            string? raw =
                User?.FindFirst("business_id")?.Value ??
                User?.FindFirst("BusinessId")?.Value ??
                User?.FindFirst("businessId")?.Value ??
                Request.Headers["X-Business-Id"].FirstOrDefault();

            if (!Guid.TryParse(raw, out var id))
                throw new UnauthorizedAccessException("Business context missing.");
            return id;
        }

        // local result shape for FromSqlRaw
        private sealed class PercentileRow
        {
            public double? p50 { get; set; }
            public double? p95 { get; set; }
            public double? p99 { get; set; }
        }
    }
}


//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using xbytechat.api.Features.CampaignModule.DTOs;
//using xbytechat.api.Features.CampaignModule.Progress.Dtos;

//namespace xbytechat.api.Features.CampaignModule.Progress.Controllers
//{
//    [ApiController]
//    [Route("campaigns")]
//    public sealed class CampaignProgressController : ControllerBase
//    {
//        private readonly AppDbContext _db;

//        public CampaignProgressController(AppDbContext db) => _db = db;

//        [HttpGet("{campaignId:guid}/progress")]
//        public async Task<ActionResult<CampaignProgressDto>> GetProgress(Guid campaignId, CancellationToken ct)
//        {
//            // 1) Queue/job status from OutboundMessageJobs
//            var baseQuery = _db.OutboundMessageJobs
//                .AsNoTracking()
//                .Where(j => j.CampaignId == campaignId);

//            var total = await baseQuery.CountAsync(ct);

//            var byStatus = await baseQuery
//                .GroupBy(j => j.Status)
//                .Select(g => new { Status = g.Key, Count = g.Count() })
//                .ToListAsync(ct);

//            int pending = byStatus.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0;
//            int inflight = byStatus.FirstOrDefault(x => x.Status == "InFlight")?.Count ?? 0;
//            int sent = byStatus.FirstOrDefault(x => x.Status == "Sent")?.Count ?? 0;
//            int failed = byStatus.FirstOrDefault(x => x.Status == "Failed")?.Count ?? 0;
//            int dead = byStatus.FirstOrDefault(x => x.Status == "Dead")?.Count ?? 0;

//            // 2) Last-hour send latency from MessageLogs (Sent rows only)
//            var since = DateTime.UtcNow.AddHours(-1);
//            var recent = await _db.MessageLogs.AsNoTracking()
//                .Where(m => m.CampaignId == campaignId && m.SentAt != null && m.CreatedAt >= since)
//                .Select(m => new { m.CreatedAt, m.SentAt })
//                .ToListAsync(ct);

//            double? p50 = null, p95 = null, p99 = null;
//            if (recent.Count > 0)
//            {
//                var latenciesMs = new List<double>(recent.Count);
//                foreach (var r in recent)
//                {
//                    var ts = r.SentAt!.Value - r.CreatedAt;
//                    latenciesMs.Add(ts.TotalMilliseconds);
//                }
//                latenciesMs.Sort();

//                double Percentile(List<double> xs, double p)
//                {
//                    if (xs.Count == 0) return 0;
//                    var rank = (p / 100.0) * (xs.Count - 1);
//                    var lo = (int)Math.Floor(rank);
//                    var hi = (int)Math.Ceiling(rank);
//                    if (lo == hi) return xs[lo];
//                    var frac = rank - lo;
//                    return xs[lo] + (xs[hi] - xs[lo]) * frac;
//                }

//                p50 = Percentile(latenciesMs, 50);
//                p95 = Percentile(latenciesMs, 95);
//                p99 = Percentile(latenciesMs, 99);
//            }

//            var dto = new CampaignProgressDto
//            {
//                CampaignId = campaignId,
//                TotalJobs = total,
//                Pending = pending,
//                InFlight = inflight,
//                Sent = sent,
//                Failed = failed,
//                Dead = dead,
//                P50ms = p50,
//                P95ms = p95,
//                P99ms = p99,
//                RetrievedAtUtc = DateTime.UtcNow
//            };

//            return Ok(dto);
//        }
//    }
//}


//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using xbytechat.api.AuthModule.Models;
//using xbytechat.api.Features.CampaignModule.DTOs;

//namespace xbytechat.api.Features.CampaignModule.Controllers
//{
//    // NOTE: route is plural to match your frontend: /api/campaigns/{id}/progress
//    [ApiController]
//    [Route("api/campaigns")]
//    public class CampaignsProgressController : ControllerBase
//    {
//        private readonly AppDbContext _db;

//        public CampaignsProgressController(AppDbContext db) => _db = db;

//        [HttpGet("{id:guid}/progress")]
//        public async Task<ActionResult<CampaignProgressDto>> GetProgress([FromRoute] Guid id)
//        {
//            var businessId = GetBusinessIdOrThrow();

//            // 1) Aggregate OutboundMessageJobs for this campaign
//            var jobsQuery = _db.OutboundMessageJobs
//                .AsNoTracking()
//                .Where(j => j.BusinessId == businessId && j.CampaignId == id);

//            var total = await jobsQuery.CountAsync();

//            // group-by status safely (statuses are strings in your worker: Pending/InFlight/Sent/Failed)
//            var grouped = await jobsQuery
//                .GroupBy(j => j.Status)
//                .Select(g => new { Status = g.Key, Count = g.Count() })
//                .ToListAsync();

//            int pending = grouped.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0;
//            int inFlight = grouped.FirstOrDefault(x => x.Status == "InFlight")?.Count ?? 0;
//            int sent = grouped.FirstOrDefault(x => x.Status == "Sent")?.Count ?? 0;
//            int failed = grouped.FirstOrDefault(x => x.Status == "Failed")?.Count ?? 0;

//            // "Dead" = failed attempts that exceeded the worker retry policy.
//            // Your worker uses DEFAULT_MAX_ATTEMPTS = 3; mark those as dead for visibility.
//            const int MAX_ATTEMPTS = 3;
//            int dead = await jobsQuery.Where(j => j.Status == "Failed" && j.Attempt >= MAX_ATTEMPTS).CountAsync();

//            var completed = sent + failed; // treat both as terminal
//            var pct = total > 0 ? (completed * 100.0) / total : 0.0;

//            // 2) Percentiles from MessageLogs over the last hour (SentAt or CreatedAt → latency)
//            // We’ll compute duration_ms = (COALESCE(SentAt, CreatedAt) - CreatedAt) in milliseconds
//            // and use PostgreSQL percentile_cont via raw SQL. If DB isn’t PostgreSQL, you can replace with a fallback.

//            double? p50 = null, p95 = null, p99 = null;

//            try
//            {
//                var sql = @"
//SELECT
//  percentile_cont(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p50,
//  percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p95,
//  percentile_cont(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (COALESCE(""SentAt"", ""CreatedAt"") - ""CreatedAt"")) * 1000.0) AS p99
//FROM ""MessageLogs""
//WHERE ""BusinessId"" = {0}
//  AND ""CampaignId"" = {1}
//  AND ""Source"" = 'campaign'
//  AND ""CreatedAt"" >= NOW() - INTERVAL '1 hour';
//";
//                var row = await _db.Set<PercentileRow>()
//                    .FromSqlRaw(sql, businessId, id)
//                    .AsNoTracking()
//                    .FirstOrDefaultAsync();

//                if (row != null)
//                {
//                    p50 = row.p50;
//                    p95 = row.p95;
//                    p99 = row.p99;
//                }
//            }
//            catch
//            {
//                // Swallow percentile errors (e.g., not Postgres); UI shows "–" if null.
//            }

//            var dto = new CampaignProgressDto
//            {
//                CampaignId = id,
//                TotalJobs = total,
//                Pending = pending,
//                InFlight = inFlight,
//                Sent = sent,
//                Failed = failed,
//                Dead = dead,
//                Completed = completed,
//                CompletionPct = Math.Round(pct, 2),
//                P50ms = p50,
//                P95ms = p95,
//                P99ms = p99,
//                RetrievedAtUtc = DateTime.UtcNow
//            };

//            return Ok(dto);
//        }

//        private Guid GetBusinessIdOrThrow()
//        {
//            string? raw =
//                User?.FindFirst("business_id")?.Value ??
//                User?.FindFirst("BusinessId")?.Value ??
//                User?.FindFirst("businessId")?.Value ??
//                Request.Headers["X-Business-Id"].FirstOrDefault();

//            if (!Guid.TryParse(raw, out var id))
//                throw new UnauthorizedAccessException("Business context missing.");
//            return id;
//        }

//        // local result shape for FromSqlRaw
//        private sealed class PercentileRow
//        {
//            public double? p50 { get; set; }
//            public double? p95 { get; set; }
//            public double? p99 { get; set; }
//        }
//    }
//}
