using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // for User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/queue")]
    [Authorize]
    public class OutboundCampaignQueueController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IOutboundCampaignQueueService _queue;

        public OutboundCampaignQueueController(AppDbContext db, IOutboundCampaignQueueService queue)
        {
            _db = db; _queue = queue;
        }

        // GET: /api/campaigns/{id}/queue/jobs
        [HttpGet("jobs")]
        public async Task<ActionResult<IEnumerable<OutboundCampaignJobDto>>> ListJobs(Guid campaignId)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // ownership check
            var exists = await _db.Campaigns.AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId);
            if (!exists) return NotFound();

            var jobs = await _queue.GetJobsForCampaignAsync(businessId, campaignId);
            return Ok(jobs.Select(Map));
        }

        // POST: /api/campaigns/{id}/queue/enqueue?forceDuplicate=false
        [HttpPost("enqueue")]
        public async Task<ActionResult<object>> Enqueue(Guid campaignId, [FromQuery] bool forceDuplicate = false)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var exists = await _db.Campaigns.AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId);
            if (!exists) return NotFound();

            var jobId = await _queue.EnqueueAsync(businessId, campaignId, forceDuplicate);
            return Ok(new { success = true, jobId });
        }

        // POST: /api/campaigns/{id}/queue/{jobId}/retry
        [HttpPost("{jobId:guid}/retry")]
        public async Task<ActionResult<object>> Retry(Guid campaignId, Guid jobId)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // Optional: ensure job belongs to this campaign & business
            var job = await _db.OutboundCampaignJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.BusinessId == businessId && j.CampaignId == campaignId);

            if (job == null) return NotFound();

            var ok = await _queue.ForceRetryNowAsync(businessId, jobId);
            return Ok(new { success = ok });
        }

        // POST: /api/campaigns/{id}/queue/{jobId}/cancel
        [HttpPost("{jobId:guid}/cancel")]
        public async Task<ActionResult<object>> Cancel(Guid campaignId, Guid jobId)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var job = await _db.OutboundCampaignJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.BusinessId == businessId && j.CampaignId == campaignId);

            if (job == null) return NotFound();

            var ok = await _queue.CancelAsync(businessId, jobId);
            return Ok(new { success = ok });
        }

        private static OutboundCampaignJobDto Map(OutboundCampaignJob j) => new OutboundCampaignJobDto
        {
            Id = j.Id,
            BusinessId = j.BusinessId,
            CampaignId = j.CampaignId,
            Status = j.Status,
            Attempt = j.Attempt,
            MaxAttempts = j.MaxAttempts,
            NextAttemptAt = j.NextAttemptAt,
            LastError = j.LastError,
            CreatedAt = j.CreatedAt,
            UpdatedAt = j.UpdatedAt
        };
    }
}
