using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Tracking.DTOs; // Updated namespace

namespace xbytechat.api.Features.Tracking.Services
{
    public interface IContactJourneyService
    {
        Task<JourneyResponseDto> GetJourneyEventsAsync(Guid initialCampaignSendLogId, CancellationToken ct = default);
    }
}