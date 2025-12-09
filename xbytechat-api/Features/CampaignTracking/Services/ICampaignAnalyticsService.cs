using xbytechat.api.Features.CampaignTracking.DTOs;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public interface ICampaignAnalyticsService
    {
       // Task<CampaignStatusDashboardDto> GetStatusDashboardAsync(Guid businessId);
        Task<IEnumerable<TopCampaignDto>> GetTopCampaignsAsync(Guid businessId, int count = 5);
        Task<CampaignStatusDashboardDto?> GetCampaignStatsAsync(Guid campaignId);
    }
}