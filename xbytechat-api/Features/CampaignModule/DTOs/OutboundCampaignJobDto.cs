using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class OutboundCampaignJobDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid CampaignId { get; set; }

        public string Status { get; set; } = "queued"; // queued | running | succeeded | failed | canceled
        public int Attempt { get; set; }
        public int MaxAttempts { get; set; }

        public DateTimeOffset? NextAttemptAt { get; set; }
        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
