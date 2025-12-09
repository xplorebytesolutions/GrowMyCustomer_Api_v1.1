namespace xbytechat.api.Features.AccountInsights.Models
{
    /// <summary>
    /// Canonical action type codes for AccountInsightsAction.ActionType.
    /// Keeps frontend and backend aligned and avoids random strings.
    /// </summary>
    public static class AccountInsightActionTypes
    {
        // Manual sales / CS actions
        public const string TagContacted = "TAG_CONTACTED";

        // Trial lifecycle (only log when an actual manual/system CHANGE happens)
        public const string ExtendTrial = "EXTEND_TRIAL";

        // Plan changes
        public const string PlanUpgraded = "PLAN_UPGRADED";
        public const string PlanDowngraded = "PLAN_DOWNGRADED";

        // System nudges (emails / campaigns etc.)
        public const string SystemNudge = "SYSTEM_NUDGE";

        // Generic manual note
        public const string Note = "NOTE";
    }
}
