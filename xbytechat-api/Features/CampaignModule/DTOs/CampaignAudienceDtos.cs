using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    /// <summary>
    /// Current CSV audience attachment state for a campaign (active attachment only).
    /// Returned by GET /api/campaigns/{campaignId}/audience.
    /// </summary>
    public sealed class CampaignAudienceDto
    {
        public Guid? AttachmentId { get; set; }
        public Guid? AudienceId { get; set; }
        public string? AudienceName { get; set; }

        public Guid? CsvBatchId { get; set; }
        public string? FileName { get; set; }
        public DateTime? CreatedAt { get; set; }

        public int MemberCount { get; set; }

        /// <summary>
        /// True when the campaign has already been sent (or has send logs) and the audience is immutable.
        /// </summary>
        public bool IsLocked { get; set; }
    }

    public sealed class CampaignAudienceHistoryItemDto
    {
        public Guid AttachmentId { get; set; }
        public Guid AudienceId { get; set; }
        public string? AudienceName { get; set; }

        public Guid CsvBatchId { get; set; }
        public string? FileName { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeactivatedAt { get; set; }
        public string? DeactivatedBy { get; set; }
    }

    public sealed class CampaignAudienceReplaceResponseDto
    {
        public CampaignAudienceDto Active { get; set; } = new();
        public int CsvRecipientsInserted { get; set; }
    }

    public sealed class CampaignAudienceRemoveResponseDto
    {
        public CampaignAudienceDto Active { get; set; } = new();
        public int CsvRecipientsDeleted { get; set; }
    }
}
