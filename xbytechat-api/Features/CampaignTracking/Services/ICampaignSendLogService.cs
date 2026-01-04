using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignTracking.DTOs;
using xbytechat.api.Features.CRM.Dtos;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public interface ICampaignSendLogService
    {
        Task<PagedResult<CampaignSendLogDto>> GetLogsByCampaignIdAsync(
            Guid campaignId, string? status, string? search, int page, int pageSize);

        Task<List<CampaignSendLogDto>> GetLogsForContactAsync(Guid campaignId, Guid contactId);

        Task<bool> AddSendLogAsync(CampaignSendLogDto dto, string ipAddress, string userAgent);

        Task<bool> UpdateDeliveryStatusAsync(Guid logId, string status, DateTime? deliveredAt, DateTime? readAt);

        Task<bool> TrackClickAsync(Guid logId, string clickType);

        Task<CampaignLogSummaryDto> GetCampaignSummaryAsync(
            Guid campaignId,
            DateTime? fromUtc,
            DateTime? toUtc,
            int repliedWindowDays,
            Guid? runId);

        Task<PagedResult<CampaignContactListItemDto>> GetContactsByStatBucketAsync(
            Guid campaignId,
            string bucket,
            DateTime? fromUtc,
            DateTime? toUtc,
            int repliedWindowDays,
            Guid? runId,
            string? search,
            int page,
            int pageSize);
    }
}
