using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignRetargetService
    {
        Task<RetargetCampaignResponseDto> CreateRetargetCampaignAsync(
            Guid businessId,
            RetargetCampaignRequestDto dto,
            string createdBy,
            CancellationToken ct);
    }
}
