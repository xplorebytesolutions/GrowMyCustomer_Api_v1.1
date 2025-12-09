using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
// DO NOT import a different interface namespace; the interface is in this same namespace.
using xbytechat.api.Features.Queueing.DTOs;     // OutboundCampaignJobCreateDto

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Picks materialized recipients (MaterializedAt != null) in a stable order,
    /// filters to "ready" statuses, and enqueues send jobs via your outbound queue.
    /// </summary>
    public sealed class CampaignDispatcher : ICampaignDispatcher
    {
        private readonly AppDbContext _db;
        private readonly IOutboundCampaignQueueService _queue; // interface is in this same namespace

        // If you use enums, map these accordingly.
        private static readonly string[] ReadyStatuses = { "Pending", "Ready" };

        public CampaignDispatcher(AppDbContext db, IOutboundCampaignQueueService queue)
        {
            _db = db;
            _queue = queue;
        }

        //public async Task<CampaignDispatchResponseDto> DispatchAsync(
        //    Guid businessId,
        //    Guid campaignId,
        //    string mode,
        //    int count,
        //    CancellationToken ct = default)
        //{
        //    if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
        //    if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required.");

        //    mode = (mode ?? "canary").Trim().ToLowerInvariant();
        //    if (mode != "canary" && mode != "full") mode = "canary";
        //    if (count <= 0) count = 25;

        //    // 1) Sanity: ownership
        //    var owns = await _db.Campaigns.AsNoTracking()
        //        .AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);
        //    if (!owns) throw new UnauthorizedAccessException("Campaign not found or not owned by this business.");

        //    // 2) Base recipients query (materialized + ready)
        //    var baseQuery = _db.CampaignRecipients.AsNoTracking()
        //        .Where(r => r.BusinessId == businessId
        //                 && r.CampaignId == campaignId
        //                 && r.MaterializedAt != null
        //                 && ReadyStatuses.Contains(r.Status));

        //    // 3) Stable order: oldest materialized first; then Id as tiebreaker
        //    baseQuery = baseQuery.OrderBy(r => r.MaterializedAt).ThenBy(r => r.Id);

        //    // 4) Select candidates (slightly over-select; queue dedupes)
        //    var desired = mode == "canary" ? count : int.MaxValue;
        //    var take = Math.Min(desired * 2, 5000);
        //    var candidates = await baseQuery.Take(take).ToListAsync(ct);

        //    // --- Build queue jobs ---
        //    var jobs = new List<OutboundCampaignJobCreateDto>(candidates.Count);
        //    foreach (var r in candidates)
        //    {
        //        jobs.Add(new OutboundCampaignJobCreateDto
        //        {
        //            BusinessId = businessId,
        //            CampaignId = campaignId,
        //            CampaignRecipientId = r.Id,
        //            IdempotencyKey = r.IdempotencyKey
        //        });
        //    }

        //    // 5) Enqueue (no-op adapter will just count)
        //    var enqueued = await _queue.EnqueueBulkAsync(jobs, ct);

        //    // --- Prepare response ---

        //    // Fetch phones for the sample (phone is on AudienceMember, not CampaignRecipient)
        //    var memberIds = candidates
        //        .Where(r => r.AudienceMemberId.HasValue)
        //        .Select(r => r.AudienceMemberId!.Value)
        //        .Distinct()
        //        .ToList();

        //    var phoneByMemberId = await _db.AudiencesMembers.AsNoTracking()
        //        .Where(m => m.BusinessId == businessId && memberIds.Contains(m.Id))
        //        .Select(m => new { m.Id, m.PhoneE164 })
        //        .ToDictionaryAsync(x => x.Id, x => x.PhoneE164, ct);

        //    var resp = new CampaignDispatchResponseDto
        //    {
        //        CampaignId = campaignId,
        //        Mode = mode,
        //        RequestedCount = count,
        //        SelectedCount = candidates.Count,
        //        EnqueuedCount = enqueued,
        //        Sample = candidates
        //            .Take(10)
        //            .Select(r => new DispatchedRecipientDto
        //            {
        //                RecipientId = r.Id,
        //                Phone = (r.AudienceMemberId.HasValue &&
        //                         phoneByMemberId.TryGetValue(r.AudienceMemberId.Value, out var p))
        //                            ? p
        //                            : null,
        //                Status = r.Status,
        //                MaterializedAt = r.MaterializedAt,
        //                IdempotencyKey = r.IdempotencyKey
        //            })
        //            .ToList()
        //    };

        //    if (mode == "full")
        //    {
        //        resp.Warnings.Add("Full dispatch requested; rate limiting/backoff is enforced by the worker/queue.");
        //    }

        //    Log.Information("Dispatch queued {@Summary}", new
        //    {
        //        businessId,
        //        campaignId,
        //        mode,
        //        requested = count,
        //        selected = candidates.Count,
        //        enqueued
        //    });

        //    return resp;
        //}

        public async Task<CampaignDispatchResponseDto> DispatchAsync(
    Guid businessId,
    Guid campaignId,
    string mode,
    int count,
    CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required.");

            mode = (mode ?? "canary").Trim().ToLowerInvariant();
            if (mode != "canary" && mode != "full") mode = "canary";
            if (count <= 0) count = 25;

            var owns = await _db.Campaigns.AsNoTracking()
                .AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);
            if (!owns) throw new UnauthorizedAccessException("Campaign not found or not owned by this business.");

            var baseQuery = _db.CampaignRecipients.AsNoTracking()
                .Where(r => r.BusinessId == businessId
                         && r.CampaignId == campaignId
                         && r.MaterializedAt != null
                         && ReadyStatuses.Contains(r.Status))
                .OrderBy(r => r.MaterializedAt).ThenBy(r => r.Id);

            var desired = mode == "canary" ? count : int.MaxValue;
            var take = Math.Min(desired * 2, 5000);
            var candidates = await baseQuery.Take(take).ToListAsync(ct);

            var jobs = new List<OutboundCampaignJobCreateDto>(candidates.Count);
            foreach (var r in candidates)
            {
                jobs.Add(new OutboundCampaignJobCreateDto
                {
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    CampaignRecipientId = r.Id,
                    IdempotencyKey = r.IdempotencyKey
                });
            }
            var enqueued = await _queue.EnqueueBulkAsync(jobs, ct);

            // ---- CHANGED: AudienceMemberId is Guid (non-nullable)
            //var memberIds = candidates
            //    .Select(r => r.AudienceMemberId)
            //    .Distinct()
            //    .ToHashSet(); // perf for Contains

            var memberIds = candidates
    .Where(r => r.AudienceMemberId.HasValue)
    .Select(r => r.AudienceMemberId!.Value)
    .Distinct()
    .ToHashSet();

            var phoneByMemberId = await _db.AudienceMembers   // or _db.AudiencesMembers if that's your DbSet
      .AsNoTracking()
      .Where(m => m.BusinessId == businessId && memberIds.Contains(m.Id))
      .Select(m => new { m.Id, m.PhoneE164 })
      .ToDictionaryAsync(x => x.Id, x => x.PhoneE164, ct);

            var resp = new CampaignDispatchResponseDto
            {
                CampaignId = campaignId,
                Mode = mode,
                RequestedCount = count,
                SelectedCount = candidates.Count,
                EnqueuedCount = enqueued,
                Sample = candidates
                .Take(10)
                .Select(r => new DispatchedRecipientDto
                {
                    RecipientId = r.Id,
                    Phone = (r.AudienceMemberId.HasValue &&
                             phoneByMemberId.TryGetValue(r.AudienceMemberId.Value, out var p))
                                ? p
                                : null,
                    Status = r.Status,
                    MaterializedAt = r.MaterializedAt,
                    IdempotencyKey = r.IdempotencyKey
                })
                .ToList()
            };

            if (mode == "full")
                resp.Warnings.Add("Full dispatch requested; rate limiting/backoff is enforced by the worker/queue.");

            Log.Information("Dispatch queued {@Summary}", new { businessId, campaignId, mode, requested = count, selected = candidates.Count, enqueued });
            return resp;
        }

    }
}
