using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignRecipientService
    {
        Task<CampaignRecipientDto?> GetByIdAsync(Guid businessId, Guid id);
        Task<List<CampaignRecipientDto>> GetByCampaignIdAsync(Guid businessId, Guid campaignId);

        Task<bool> UpdateStatusAsync(Guid businessId, Guid recipientId, string newStatus);
        Task<bool> TrackReplyAsync(Guid businessId, Guid recipientId, string replyText);
        Task<List<CampaignRecipientDto>> SearchRecipientsAsync(Guid businessId, string? status = null, string? keyword = null);

        Task AssignContactsToCampaignAsync(Guid businessId, Guid campaignId, List<Guid> contactIds);
    }
}
