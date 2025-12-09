using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public partial class CampaignService
    {
        /// <summary>
        /// Sends a text/template campaign to a given subset of recipient IDs (batch).
        /// Leverages the same pipeline & idempotency you just implemented.
        /// </summary>
        public async Task<ResponseResult> SendTemplateCampaignBatchAsync(Guid campaignId, IEnumerable<Guid> recipientIds)
        {
            if (campaignId == Guid.Empty) return ResponseResult.ErrorInfo("Invalid campaign id.");
            var ids = recipientIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
            if (ids.Count == 0) return ResponseResult.ErrorInfo("No recipients to send in this batch.");

            var campaign = await _context.Campaigns
                .Include(c => c.Recipients.Where(r => ids.Contains(r.Id))).ThenInclude(r => r.Contact)
                .Include(c => c.MultiButtons)
                .FirstOrDefaultAsync(c => c.Id == campaignId && !c.IsDeleted);

            if (campaign == null) return ResponseResult.ErrorInfo("Campaign not found.");
            if (campaign.Recipients == null || campaign.Recipients.Count == 0)
                return ResponseResult.ErrorInfo("No batch recipients matched this campaign.");

            // Reuse your existing method that sends a single campaign object with its recipients loaded.
            // It already handles: provider resolution, template meta, freezing params/URLs,
            // idempotency key, logs, and billing ingest.
            return await SendTextTemplateCampaignAsync(campaign);
        }
    }
}
