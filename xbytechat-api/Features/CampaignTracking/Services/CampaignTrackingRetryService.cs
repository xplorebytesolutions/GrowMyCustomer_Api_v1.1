using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignTracking.Models;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public class CampaignTrackingRetryService : ICampaignTrackingRetryService
    {
        private readonly AppDbContext _context;

        public CampaignTrackingRetryService(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Public method: Retry a single failed log
        public async Task<bool> RetrySingleAsync(Guid logId)
        {
            return await RetrySendLogAsync(logId);
        }

        // ✅ Public method: Retry all failed logs in a campaign
        public async Task<int> RetryFailedInCampaignAsync(Guid campaignId)
        {
            return await RetryAllFailedInCampaignAsync(campaignId);
        }

        // 🔁 Private: Retry a specific log
        private async Task<bool> RetrySendLogAsync(Guid logId)
        {
            var log = await _context.CampaignSendLogs.FirstOrDefaultAsync(x => x.Id == logId);

            if (log == null || log.SendStatus != "Failed" || !log.AllowRetry)
                return false;

            // 🔄 Simulate re-send (replace with actual IMessageService.SendAsync later)
            bool sent = SimulateSendMessage(log);

            log.RetryCount += 1;
            log.LastRetryAt = DateTime.UtcNow;
            log.LastRetryStatus = sent ? "Sent" : "Failed";
            log.SendStatus = sent ? "Sent" : "Failed";
            log.ErrorMessage = sent ? null : "Mock failure on retry";

            await _context.SaveChangesAsync();
            return sent;
        }

        // 🔁 Private: Retry all failed logs in a given campaign
        private async Task<int> RetryAllFailedInCampaignAsync(Guid campaignId)
        {
            var failedLogs = await _context.CampaignSendLogs
                .Where(log => log.CampaignId == campaignId && log.SendStatus == "Failed" && log.AllowRetry)
                .ToListAsync();

            int successCount = 0;

            foreach (var log in failedLogs)
            {
                bool sent = SimulateSendMessage(log);

                log.RetryCount += 1;
                log.LastRetryAt = DateTime.UtcNow;
                log.LastRetryStatus = sent ? "Sent" : "Failed";
                log.SendStatus = sent ? "Sent" : "Failed";
                log.ErrorMessage = sent ? null : "Mock failure on retry";

                if (sent) successCount++;
            }

            await _context.SaveChangesAsync();
            return successCount;
        }

        // 🔧 Simulated send (replace with actual WhatsApp message logic)
        private bool SimulateSendMessage(CampaignSendLog log)
        {
            return new Random().NextDouble() < 0.9; // 90% success rate
        }
    }
}
