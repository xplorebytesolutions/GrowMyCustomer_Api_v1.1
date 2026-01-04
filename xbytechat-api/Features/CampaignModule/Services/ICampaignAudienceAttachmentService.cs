using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignAudienceAttachmentService
    {
        Task<CampaignAudienceDto> GetActiveAsync(Guid businessId, Guid campaignId, CancellationToken ct = default);
        Task<IReadOnlyList<CampaignAudienceHistoryItemDto>> GetHistoryAsync(Guid businessId, Guid campaignId, CancellationToken ct = default);

        /// <summary>
        /// Replaces the active CSV audience before send. This deactivates the previous attachment (history is preserved),
        /// rebuilds ONLY CSV-derived recipients (AudienceMemberId != null), and leaves manual recipients intact.
        /// </summary>
        Task<CampaignAudienceReplaceResponseDto> ReplaceAsync(
            Guid businessId,
            Guid campaignId,
            IFormFile csvFile,
            string? audienceName,
            string actor,
            CancellationToken ct = default);

        /// <summary>
        /// Removes/detaches the active CSV audience before send. This deactivates the attachment (history is preserved)
        /// and deletes ONLY CSV-derived recipients (AudienceMemberId != null).
        /// </summary>
        Task<CampaignAudienceRemoveResponseDto> RemoveAsync(
            Guid businessId,
            Guid campaignId,
            string actor,
            CancellationToken ct = default);

        /// <summary>
        /// Shared persist path for POST /api/campaigns/{campaignId}/materialize when Persist=true.
        /// This keeps attachments + CSV-derived recipients aligned with what the user just materialized.
        /// </summary>
        Task<CampaignAudienceReplaceResponseDto> ReplaceFromMaterializationAsync(
            Guid businessId,
            Guid campaignId,
            CampaignCsvMaterializeRequestDto request,
            IReadOnlyList<CsvMaterializedRowDto> materializedRows,
            string actor,
            CancellationToken ct = default);
    }
}
