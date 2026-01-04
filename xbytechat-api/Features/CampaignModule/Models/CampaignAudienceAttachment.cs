// 📄 Features/CampaignModule/Models/CampaignAudienceAttachment.cs
using System;
using xbytechat.api.Features.BusinessModule.Models;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class CampaignAudienceAttachment
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }
        public Business Business { get; set; } = default!;

        public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = default!;

        public Guid AudienceId { get; set; }
        public Audience Audience { get; set; } = default!;

        public Guid CsvBatchId { get; set; }
        public CsvBatch CsvBatch { get; set; } = default!;

        // ✅ "only one active attachment per campaign"
        public bool IsActive { get; set; } = true;

        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When replaced/disabled
        public DateTime? DeactivatedAt { get; set; }
        public string? DeactivatedBy { get; set; }
    }
}
