#nullable enable
using System;
using System.Collections.Generic;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.Payment.Models
{
    /// <summary>
    /// Represents the subscription of a Business to a specific plan.
    /// Drives access control, trials, renewals, and suspension.
    /// </summary>
    public class Subscription
    {
        /// <summary>
        /// Primary key for the subscription.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The business (account) this subscription belongs to.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// The plan the business is subscribed to.
        /// This should reference your PlanManagement module.
        /// </summary>
        public Guid PlanId { get; set; }

        /// <summary>
        /// Current lifecycle state of the subscription.
        /// </summary>
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// Billing frequency (e.g. Monthly / Yearly).
        /// </summary>
        public BillingCycle BillingCycle { get; set; }

        /// <summary>
        /// When the current billing period starts (UTC).
        /// </summary>
        public DateTime CurrentPeriodStartUtc { get; set; }

        /// <summary>
        /// When the current billing period ends (UTC).
        /// </summary>
        public DateTime CurrentPeriodEndUtc { get; set; }

        /// <summary>
        /// Optional trial end (UTC). When passed and not activated -> Expired.
        /// </summary>
        public DateTime? TrialEndsAtUtc { get; set; }

        /// <summary>
        /// If true, subscription will auto-renew at the end of each period.
        /// </summary>
        public bool AutoRenew { get; set; } = true;

        /// <summary>
        /// If true, subscription will be cancelled when the current period ends.
        /// </summary>
        public bool CancelAtPeriodEnd { get; set; }

        /// <summary>
        /// Gateway specific customer id (Stripe, Razorpay, etc) for this business.
        /// Stored here so multiple subscriptions can still share mapping if needed.
        /// </summary>
        public string? GatewayCustomerId { get; set; }

        /// <summary>
        /// Gateway specific subscription id, if managed on the gateway side.
        /// </summary>
        public string? GatewaySubscriptionId { get; set; }

        /// <summary>
        /// Optional internal notes (manual overrides, special deals, etc.).
        /// </summary>
        public string? Notes { get; set; }

        // ---- Navigation properties ----

        public Business? Business { get; set; }          // from BusinessModule
        public Plan? Plan { get; set; }                  // from PlanManagement
        public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
