using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignMaterializer
    {
        Task<CampaignCsvMaterializeResponseDto> CreateAsync(
            Guid businessId,
            Guid campaignId,
            CampaignCsvMaterializeRequestDto request,
            string actor,
            CancellationToken ct = default);
    }
}
