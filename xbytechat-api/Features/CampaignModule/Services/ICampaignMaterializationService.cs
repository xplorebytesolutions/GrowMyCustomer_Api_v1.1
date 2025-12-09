using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignMaterializationService
    {
        Task<CampaignMaterializeResultDto> MaterializeAsync(
        Guid businessId,
        Guid campaignId,
        int limit = 200,
            CancellationToken ct = default);
    }
}
