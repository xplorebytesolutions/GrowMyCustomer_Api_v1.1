using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignDispatcher
    {
        /// <summary>
        /// mode: "canary" (use count) or "full" (ignore count, select all ready).
        /// count: when mode=canary, number of recipients to enqueue (default 25).
        /// </summary>
        Task<CampaignDispatchResponseDto> DispatchAsync(
            Guid businessId,
            Guid campaignId,
            string mode,
            int count,
            CancellationToken ct = default);
    }
}
