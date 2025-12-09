using System;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public interface ICampaignTrackingRetryService
    {

        // Retry a single failed message log by its ID.
        // will be used when user clicks "Retry Now" on a log row.
        Task<bool> RetrySingleAsync(Guid logId);


        // Retry all failed messages in a campaign where retry is allowed.
        // will support "Retry All Failed" button from Campaign logs.
        Task<int> RetryFailedInCampaignAsync(Guid campaignId);
    }
}
