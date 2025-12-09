namespace xbytechat.api.Features.AccountInsights.Models
{
    public enum AccountLifecycleStage
    {
        Unknown = 0,

        PendingApproval = 10,      // Signed up, not yet approved
        Rejected = 20,             // Explicitly rejected
        InactiveDeleted = 25,      // Soft-deleted

        Trial = 30,                // Trial plan, approved, some activity or in trial window
        Active = 40,               // Approved + active usage
        AtRisk = 50,               // Approved + low/no recent usage
        Dormant = 60,              // Approved but idle for a long time

        NoUsagePostApproval = 70   // Approved but literally never used (high churn risk)
    }
}
