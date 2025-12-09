#nullable enable
using System;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    /// <summary>
    /// Read model for exposing subscription info to UI / API.
    /// </summary>
    public class SubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid PlanId { get; set; }

        public string PlanName { get; set; } = string.Empty;
        public SubscriptionStatus Status { get; set; }
        public BillingCycle BillingCycle { get; set; }

        public DateTime CurrentPeriodStartUtc { get; set; }
        public DateTime CurrentPeriodEndUtc { get; set; }
        public DateTime? TrialEndsAtUtc { get; set; }

        public bool AutoRenew { get; set; }
        public bool CancelAtPeriodEnd { get; set; }

        public string? GatewayCustomerId { get; set; }
        public string? GatewaySubscriptionId { get; set; }
    }
}
