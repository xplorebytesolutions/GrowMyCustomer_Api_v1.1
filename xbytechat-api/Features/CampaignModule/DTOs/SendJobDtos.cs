using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public enum SendJobState
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Canceled = 4,
        Partial = 5
    }

    public class SendJobStartRequestDto
    {
        public bool Force { get; set; } = false;     // allow send even if dry-run has errors (logged loudly)
        public int Limit { get; set; } = 2000;       // cap on planned recipients
    }

    public class SendJobStartResponseDto
    {
        public Guid JobId { get; set; }
        public Guid CampaignId { get; set; }
        public string Message { get; set; } = "Send job queued.";
    }

    public class SendJobBatchResultDto
    {
        public int BatchNumber { get; set; }
        public int Count { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int OffsetSeconds { get; set; }
    }

    public class SendJobStatusDto
    {
        public Guid JobId { get; set; }
        public Guid CampaignId { get; set; }
        public SendJobState State { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

        public int PlannedBatches { get; set; }
        public int CompletedBatches { get; set; }

        public int PlannedRecipients { get; set; }
        public int SentSuccess { get; set; }
        public int SentFailed { get; set; }
        public int Skipped { get; set; }

        public List<SendJobBatchResultDto> Batches { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
