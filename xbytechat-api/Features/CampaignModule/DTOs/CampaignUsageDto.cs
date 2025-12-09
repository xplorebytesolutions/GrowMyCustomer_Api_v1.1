using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignUsageDto
    {
        public Guid CampaignId { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT/SCHEDULED/QUEUED/SENDING/SENT/...
        public int Recipients { get; set; }
        public int QueuedJobs { get; set; }
        public int SendLogs { get; set; }

        // Flow linkage (for parity with CTA Flow deletion UX)
        public bool HasFlow { get; set; }
        public Guid? FlowId { get; set; }

        // Useful timestamps for UX copy
        public DateTime? CreatedAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? FirstSentAt { get; set; }
    }
}
