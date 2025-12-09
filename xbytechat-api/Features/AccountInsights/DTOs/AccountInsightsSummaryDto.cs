using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AccountInsights.DTOs
{
    public class AccountInsightsSummaryDto
    {
        public DateTime GeneratedAtUtc { get; set; }

        // Core counts
        public int TotalBusinesses { get; set; }
        public int ActiveBusinesses { get; set; }
        public int AtRiskBusinesses { get; set; }
        public int DormantBusinesses { get; set; }
        public int NoUsagePostApproval { get; set; }

        public int PendingApproval { get; set; }
        public int Rejected { get; set; }
        public int Deleted { get; set; }

        // Plan
        public int TrialPlan { get; set; }
        public int PaidPlan { get; set; }
        public int UnknownPlan { get; set; }

        // Trial lifecycle
        public int TrialTotal { get; set; }
        public int TrialExpiringSoon { get; set; }
        public int TrialExpiredNoUpgrade { get; set; }

        // Distributions
        public Dictionary<string, int> ByLifecycleStage { get; set; } = new();
        public Dictionary<string, int> ByPlanType { get; set; } = new();
    }
}
