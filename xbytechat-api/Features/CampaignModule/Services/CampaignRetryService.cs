using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;

namespace xbytechat.api.Features.CampaignModule.Services
{
 
    public sealed class CampaignRetryService : ICampaignRetryService
    {
        private readonly AppDbContext _db;
        private readonly CampaignService _campaignService; // use concrete to reach batch method

        public CampaignRetryService(AppDbContext db, ICampaignService campaignService)
        {
            _db = db;
            // We know our concrete implementation exposes the batch entry.
            _campaignService = (CampaignService)campaignService;
        }

        public async Task<CampaignRetryResultDto> RetryFailedAsync(Guid businessId, Guid campaignId, int limit = 200)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required");
            if (limit <= 0) limit = 200;

            var exists = await _db.Campaigns
                .AsNoTracking()
                .AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId && !c.IsDeleted);
            if (!exists) throw new KeyNotFoundException("Campaign not found.");

            // Most recent log per recipient, keep only those whose latest is Failed
            var failedQuery =
                from log in _db.CampaignSendLogs.AsNoTracking()
                where log.BusinessId == businessId && log.CampaignId == campaignId
                group log by log.RecipientId into g
                let last = g.OrderByDescending(x => x.CreatedAt).First()
                where last.SendStatus == "Failed"
                select new { RecipientId = last.RecipientId }; // <-- Guid (non-nullable)

            var failed = await failedQuery
                .Select(x => x.RecipientId)   // <-- no .Value
                .Distinct()
                .Take(limit)
                .ToListAsync();

            var result = new CampaignRetryResultDto
            {
                CampaignId = campaignId,
                ConsideredFailed = failed.Count
            };

            if (failed.Count == 0)
            {
                result.Note = "No failed recipients found to retry.";
                return result;
            }

            // Filter out recipients whose latest log is Sent (paranoia/safety)
            var latestOkQuery =
                from log in _db.CampaignSendLogs.AsNoTracking()
                where log.BusinessId == businessId && log.CampaignId == campaignId
                group log by log.RecipientId into g
                let last = g.OrderByDescending(x => x.CreatedAt).First()
                where last.SendStatus == "Sent"
                select new { RecipientId = last.RecipientId }; // Guid

            var alreadyOk = await latestOkQuery
                .Select(x => x.RecipientId)
                .ToListAsync();

            var toRetry = failed.Except(alreadyOk).ToList();
            result.Skipped = failed.Count - toRetry.Count;

            if (toRetry.Count == 0)
            {
                result.Note = "All failed recipients appear to have a later successful send.";
                return result;
            }

            // Send the batch via canonical pipeline (freezing + idempotency safeguard)
            var resp = await _campaignService.SendTemplateCampaignBatchAsync(campaignId, toRetry);

            result.Retried = resp.Success ? toRetry.Count : 0;
            result.Note = resp.Success ? "Retry dispatched." : ("Retry failed: " + (resp.Message ?? "Unknown error."));
            result.RecipientIdsSample = toRetry.Take(20).ToList();

            Log.Information("Campaign retry executed {@Retry}", new
            {
                businessId,
                campaignId,
                consideredFailed = result.ConsideredFailed,
                skipped = result.Skipped,
                retried = result.Retried
            });

            return result;
        }

    }
}
