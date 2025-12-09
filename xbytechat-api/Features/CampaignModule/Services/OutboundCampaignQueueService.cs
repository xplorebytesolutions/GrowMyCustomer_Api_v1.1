using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;                       // <-- add
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.Queueing.DTOs;    // <-- add for OutboundCampaignJobCreateDto

namespace xbytechat.api.Features.CampaignModule.Services
{
    public class OutboundCampaignQueueService : IOutboundCampaignQueueService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<OutboundCampaignQueueService> _log;

        public OutboundCampaignQueueService(AppDbContext db, ILogger<OutboundCampaignQueueService> log)
        {
            _db = db; _log = log;
        }

        public async Task<Guid> EnqueueAsync(Guid businessId, Guid campaignId, bool forceDuplicate = false)
        {
            if (!forceDuplicate)
            {
                var existing = await _db.OutboundCampaignJobs
                    .Where(j => j.CampaignId == campaignId && (j.Status == "queued" || j.Status == "running"))
                    .OrderByDescending(j => j.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    var found = await _db.Campaigns
                        .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

                    if (found != null && found.Status != "Queued")
                    {
                        found.Status = "Queued";
                        found.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }

                    _log.LogInformation("Campaign {CampaignId} already queued/running. Job={JobId}", campaignId, existing.Id);
                    return existing.Id;
                }
            }

            var job = new OutboundCampaignJob
            {
                BusinessId = businessId,
                CampaignId = campaignId,
                Status = "queued",
                Attempt = 0,
                MaxAttempts = 5,
                NextAttemptAt = DateTimeOffset.UtcNow
            };

            _db.OutboundCampaignJobs.Add(job);

            var row = await _db.Campaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

            if (row != null)
            {
                row.Status = "Queued";
                row.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return job.Id;
        }

        public async Task<bool> MarkSucceededAsync(Guid jobId)
        {
            var j = await _db.OutboundCampaignJobs.FindAsync(jobId);
            if (j == null) return false;

            j.Attempt += 1;
            j.Status = "succeeded";
            j.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkFailedAsync(Guid jobId, string error, bool scheduleRetry = true)
        {
            var j = await _db.OutboundCampaignJobs.FindAsync(jobId);
            if (j == null) return false;

            j.Attempt += 1;
            j.LastError = Truncate(error, 3900);
            j.UpdatedAt = DateTime.UtcNow;

            if (!scheduleRetry || j.Attempt >= j.MaxAttempts)
            {
                j.Status = "failed";
            }
            else
            {
                var backoff = j.Attempt switch
                {
                    1 => TimeSpan.FromMinutes(1),
                    2 => TimeSpan.FromMinutes(5),
                    3 => TimeSpan.FromMinutes(15),
                    4 => TimeSpan.FromMinutes(60),
                    _ => TimeSpan.FromMinutes(180)
                };
                j.Status = "queued";
                j.NextAttemptAt = DateTimeOffset.UtcNow.Add(backoff);
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<OutboundCampaignJob>> GetJobsForCampaignAsync(Guid businessId, Guid campaignId)
        {
            return await _db.OutboundCampaignJobs
                .Where(j => j.BusinessId == businessId && j.CampaignId == campaignId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task<OutboundCampaignJob?> GetActiveJobForCampaignAsync(Guid businessId, Guid campaignId)
        {
            return await _db.OutboundCampaignJobs
                .Where(j => j.BusinessId == businessId && j.CampaignId == campaignId &&
                            (j.Status == "queued" || j.Status == "running"))
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CancelAsync(Guid businessId, Guid jobId)
        {
            var j = await _db.OutboundCampaignJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.BusinessId == businessId);
            if (j == null) return false;

            if (j.Status == "queued" || j.Status == "running")
            {
                j.Status = "canceled";
                j.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == j.CampaignId && c.BusinessId == businessId);
                if (campaign != null)
                {
                    var hasActive = await _db.OutboundCampaignJobs.AnyAsync(x =>
                        x.CampaignId == j.CampaignId &&
                        x.BusinessId == businessId &&
                        (x.Status == "queued" || x.Status == "running"));

                    if (!hasActive && (campaign.Status == "Queued" || campaign.Status == "Sending"))
                    {
                        campaign.Status = "Draft";
                        campaign.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                }
                return true;
            }
            return false;
        }

        public async Task<bool> ForceRetryNowAsync(Guid businessId, Guid jobId)
        {
            var j = await _db.OutboundCampaignJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.BusinessId == businessId);
            if (j == null) return false;

            j.Status = "queued";
            j.NextAttemptAt = DateTimeOffset.UtcNow;
            j.UpdatedAt = DateTime.UtcNow;

            var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == j.CampaignId && c.BusinessId == businessId);
            if (campaign != null && campaign.Status != "Queued")
            {
                campaign.Status = "Queued";
                campaign.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        // NEW: bulk enqueue used by CampaignDispatcher
        public Task<int> EnqueueBulkAsync(IEnumerable<OutboundCampaignJobCreateDto> jobs, CancellationToken ct = default)
        {
            // For now, just log & return a deduped count. Replace with real queue later.
            var list = (jobs ?? Enumerable.Empty<OutboundCampaignJobCreateDto>()).ToList();

            // Deduplicate by provided IdempotencyKey (or fallback to a stable composite)
            var enqueuedCount = list
                .GroupBy(j => string.IsNullOrWhiteSpace(j.IdempotencyKey)
                                ? $"{j.CampaignId}:{j.CampaignRecipientId}"
                                : j.IdempotencyKey)
                .Count();

            _log.LogInformation("Bulk enqueue requested: {Requested} jobs, deduped to {Enqueued}",
                list.Count, enqueuedCount);

            // TODO: push to a real queue/bus and persist queue records as needed.
            return Task.FromResult(enqueuedCount);
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
    }
}
