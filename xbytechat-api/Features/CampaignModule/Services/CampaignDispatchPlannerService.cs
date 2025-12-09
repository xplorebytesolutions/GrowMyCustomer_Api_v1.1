using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignDispatchPlannerService
    {
        /// <summary>
        /// Build a read-only dispatch plan: batches, offsets, size estimates, and throttle summary.
        /// No messages are sent and no DB writes are performed.
        /// </summary>
        Task<CampaignDispatchPlanResultDto> PlanAsync(Guid businessId, Guid campaignId, int limit = 2000, CancellationToken ct = default);
    }

    /// <summary>
    /// Computes batch plan from materialized rows with simple throttling:
    /// - Derives batch size and per-minute cap from Business.Plan (fallback defaults).
    /// - Slices rows into batches and schedules offsets (seconds) so per-minute cap is respected.
    /// - Approximates payload size per row/batch for sanity checks.
    /// </summary>
    public class CampaignDispatchPlannerService : ICampaignDispatchPlannerService
    {
        private readonly AppDbContext _db;
        private readonly ICampaignMaterializationService _materializer;

        public CampaignDispatchPlannerService(AppDbContext db, ICampaignMaterializationService materializer)
        {
            _db = db;
            _materializer = materializer;
        }

        public async Task<CampaignDispatchPlanResultDto> PlanAsync(Guid businessId, Guid campaignId, int limit = 2000, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("CampaignId is required.");

            // Load campaign shell for meta
            var campaign = await _db.Campaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct)
                ?? throw new KeyNotFoundException("Campaign not found.");

            // Materialize (reuses Step 2.11) — read-only
            var mat = await _materializer.MaterializeAsync(businessId, campaignId, limit, ct);

            // Business plan & provider heuristics (non-fatal if missing)
            var biz = await _db.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == businessId, ct);

            // var planName = (biz?.Plan ?? "Basic").Trim();
            var planName = (biz == null
     ? "Basic"
     : (biz.Plan?.ToString() ?? "Basic")
 ).Trim();
            var provider = (campaign.Provider ?? "Auto").Trim(); // if you snapshot provider on Campaign; otherwise "Auto"

            // Throttle rules (sane defaults; adjust to your real plan matrix if available)
            var (maxBatch, perMinute) = GetThrottleForPlan(planName);

            var result = new CampaignDispatchPlanResultDto
            {
                CampaignId = campaignId,
                TemplateName = mat.TemplateName,
                Language = mat.Language,
                PlaceholderCount = mat.PlaceholderCount,
                TotalRecipients = mat.Rows.Count,
                Throttle = new DispatchThrottleDto
                {
                    Plan = planName,
                    Provider = provider,
                    MaxBatchSize = maxBatch,
                    MaxPerMinute = perMinute
                }
            };

            if (mat.Rows.Count == 0)
            {
                result.GlobalWarnings.Add("No recipients available to plan. Ensure audience or campaign recipients exist.");
                result.WarningCount = result.GlobalWarnings.Count;
                return result;
            }

            // Approx size per row (naive): sum of parameter lengths + resolved button urls + a small fixed header cost
            var approxBytesPerRow = new List<int>(mat.Rows.Count);
            foreach (var row in mat.Rows)
            {
                var paramBytes = row.Parameters.Sum(p => (p.Value?.Length ?? 0));
                var btnBytes = row.Buttons.Sum(b => (b.ResolvedUrl?.Length ?? 0) + (b.ButtonText?.Length ?? 0));
                // add a tiny constant for template envelope; tweak if you maintain captions/text
                var approx = (paramBytes + btnBytes + 64);
                approxBytesPerRow.Add(approx);
            }

            result.TotalApproxBytes = approxBytesPerRow.Sum();

            // Build batches by MaxBatchSize
            var batches = new List<DispatchBatchDto>();
            var total = mat.Rows.Count;
            var batchCount = (int)Math.Ceiling(total / (double)maxBatch);

            // Schedule offsets constrained by MaxPerMinute:
            // At most 'perMinute' messages may start within any 60-second window.
            // Strategy: bucket batches into "minutes", each minute can hold floor(perMinute / maxBatch) full batches.
            var batchesPerMinute = Math.Max(1, perMinute / Math.Max(1, maxBatch));
            if (batchesPerMinute == 0) batchesPerMinute = 1; // guard

            var offsetMinutes = 0;
            var slotInMinute = 0;
            int globalIdx = 0;

            for (int b = 0; b < batchCount; b++)
            {
                var startIndex = b * maxBatch;
                var take = Math.Min(maxBatch, total - startIndex);

                var slicePhones = new List<string?>(take);
                var sliceRecipientIds = new List<Guid?>(take);
                var sliceApprox = 0;

                for (int i = 0; i < take; i++)
                {
                    var row = mat.Rows[startIndex + i];
                    slicePhones.Add(row.Phone);
                    sliceRecipientIds.Add(row.RecipientId);
                    sliceApprox += approxBytesPerRow[startIndex + i];
                }

                var batch = new DispatchBatchDto
                {
                    BatchNumber = b + 1,
                    StartIndex = startIndex,
                    Count = take,
                    ApproxBytes = sliceApprox,
                    RecipientIds = sliceRecipientIds,
                    Phones = slicePhones,
                    OffsetSeconds = offsetMinutes * 60
                };

                // Notes for the curious
                if (slicePhones.Any(p => string.IsNullOrWhiteSpace(p)))
                    batch.Notes.Add("Some rows missing phone; those will fail at send-time unless corrected.");
                if (sliceApprox / Math.Max(1, take) > 2000)
                    batch.Notes.Add("Average payload per row is large; provider truncation risk.");

                batches.Add(batch);

                // advance slot & minute window
                slotInMinute++;
                if (slotInMinute >= batchesPerMinute)
                {
                    slotInMinute = 0;
                    offsetMinutes++;
                }

                globalIdx += take;
            }

            result.Batches = batches;
            result.Throttle.ComputedBatches = batches.Count;
            // Estimated minutes: ceil(total recipients / perMinute)
            result.Throttle.EstimatedMinutes = (int)Math.Ceiling(total / (double)Math.Max(1, perMinute));

            // Warnings
            if (perMinute < 30) result.Throttle.Warnings.Add("Low per-minute limit; delivery may be slow for large audiences.");
            if (result.TotalApproxBytes > 5_000_000) result.GlobalWarnings.Add("Plan size is large (>5MB). Consider splitting the audience.");

            result.WarningCount =
                result.GlobalWarnings.Count +
                result.Throttle.Warnings.Count +
                result.Batches.Sum(bh => bh.Notes.Count);

            Log.Information("Dispatch plan computed {@PlanSummary}",
                new
                {
                    campaignId,
                    businessId,
                    mat.TemplateName,
                    mat.Language,
                    totalRecipients = result.TotalRecipients,
                    batches = result.Batches.Count,
                    perMinute,
                    maxBatch,
                    estMinutes = result.Throttle.EstimatedMinutes
                });

            return result;
        }

        private static (int maxBatch, int perMinute) GetThrottleForPlan(string planName)
        {
            // Conservative defaults; align with your real billing/plan matrix when available.
            switch ((planName ?? "").Trim().ToLowerInvariant())
            {
                case "advanced":
                    return (100, 600);
                case "smart":
                    return (50, 300);
                case "basic":
                default:
                    return (25, 120);
            }
        }
    }
}
