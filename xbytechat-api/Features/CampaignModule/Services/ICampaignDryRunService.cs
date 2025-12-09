using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Validates a campaign for send safety without actually sending any message.
    /// </summary>
    public interface ICampaignDryRunService
    {
        /// <summary>
        /// Run dry-run validation for a campaign. Should not mutate state.
        /// </summary>
        Task<CampaignDryRunResultDto> ValidateAsync(
            Guid businessId,
            Guid campaignId,
            int limit = 200,
            CancellationToken ct = default);
    }
}
