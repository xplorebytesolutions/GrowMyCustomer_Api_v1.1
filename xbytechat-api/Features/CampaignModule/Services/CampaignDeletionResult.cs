using System;

namespace xbytechat.api.Features.CampaignModule.Services
{

    public sealed class CampaignDeletionOptions { public bool Force { get; set; } }

    public enum CampaignDeletionStatus
    {
        Deleted,
        NotFound,
        BlockedSending,
        BlockedState,
        Error
    }

    public sealed class CampaignDeletionResult
    {
        public CampaignDeletionStatus Status { get; init; }
        public int Recipients { get; init; }
        public int QueuedJobs { get; init; }
        public int SendLogs { get; init; }

        public static CampaignDeletionResult Deleted(int r, int q, int s)
            => new() { Status = CampaignDeletionStatus.Deleted, Recipients = r, QueuedJobs = q, SendLogs = s };

        public static CampaignDeletionResult NotFound()
            => new() { Status = CampaignDeletionStatus.NotFound };

        public static CampaignDeletionResult BlockedSending(int r, int q, int s)
            => new() { Status = CampaignDeletionStatus.BlockedSending, Recipients = r, QueuedJobs = q, SendLogs = s };

        public static CampaignDeletionResult BlockedState(int r, int q, int s)
            => new() { Status = CampaignDeletionStatus.BlockedState, Recipients = r, QueuedJobs = q, SendLogs = s };
    }
}
