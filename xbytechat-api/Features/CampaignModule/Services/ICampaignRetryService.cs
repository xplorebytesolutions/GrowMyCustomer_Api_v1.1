using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignRetryService
    {
        Task<CampaignRetryResultDto> RetryFailedAsync(Guid businessId, Guid campaignId, int limit = 200);
    }
}

