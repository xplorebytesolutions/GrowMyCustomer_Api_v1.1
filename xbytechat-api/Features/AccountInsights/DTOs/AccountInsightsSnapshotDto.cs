using System;
using xbytechat.api.Features.AccountInsights.Models;
using xbytechat.api.Features.PlanManagement.Models; // PlanType

namespace xbytechat.api.Features.AccountInsights.DTOs
{
    public class AccountInsightsSnapshotDto
    {
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; }
        public string BusinessEmail { get; set; }

        public bool IsDeleted { get; set; }
        public string Status { get; set; }           // Pending / Approved / Rejected (raw)
        public bool IsApproved { get; set; }

        public Guid? CreatedByPartnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // Plan / quota
        public PlanType? PlanType { get; set; }
        public int? TotalMonthlyQuota { get; set; }
        public int? RemainingMessages { get; set; }
        public DateTime? QuotaResetDate { get; set; }

        // WhatsApp / onboarding
        public bool HasWhatsAppConfig { get; set; }
        public bool HasActiveWhatsAppNumber { get; set; }

        // Usage
        public bool HasAnyMessages { get; set; }
        public DateTime? FirstMessageAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessagesLast30Days { get; set; }

        // Lifecycle
        public AccountLifecycleStage LifecycleStage { get; set; }
        public int HealthScore { get; set; }

        // Trial intelligence
        public bool IsTrial { get; set; }
        public DateTime? TrialStartAt { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public bool IsTrialExpiringSoon { get; set; }
        public bool IsTrialExpired { get; set; }
    }
}
