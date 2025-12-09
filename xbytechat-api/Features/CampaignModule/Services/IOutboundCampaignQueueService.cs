using System;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.Queueing.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface IOutboundCampaignQueueService
    {
        Task<Guid> EnqueueAsync(Guid businessId, Guid campaignId, bool forceDuplicate = false);
        Task<bool> MarkSucceededAsync(Guid jobId);
        Task<bool> MarkFailedAsync(Guid jobId, string error, bool scheduleRetry = true);

        Task<List<OutboundCampaignJob>> GetJobsForCampaignAsync(Guid businessId, Guid campaignId);
        Task<OutboundCampaignJob?> GetActiveJobForCampaignAsync(Guid businessId, Guid campaignId);
        Task<bool> CancelAsync(Guid businessId, Guid jobId);     // set to "canceled" (if queued/running)
        Task<bool> ForceRetryNowAsync(Guid businessId, Guid jobId); // set to "queued", NextAttemptAt=now (no attempt++)
        Task<int> EnqueueBulkAsync(IEnumerable<OutboundCampaignJobCreateDto> jobs, CancellationToken ct = default);

    }
}
