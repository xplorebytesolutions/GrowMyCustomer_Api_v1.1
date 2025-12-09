using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignRecipientService
    {
        Task<CampaignRecipientDto> GetByIdAsync(Guid id);
        Task<List<CampaignRecipientDto>> GetByCampaignIdAsync(Guid campaignId);

        Task<bool> UpdateStatusAsync(Guid recipientId, string newStatus);
        Task<bool> TrackReplyAsync(Guid recipientId, string replyText);
        Task<List<CampaignRecipientDto>> SearchRecipientsAsync(string status = null, string keyword = null);

        Task AssignContactsToCampaignAsync(Guid campaignId, List<Guid> contactIds);
    }
}

