#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Subscription-based access control:
    /// decides if core features (like messaging, campaigns) are allowed.
    /// All rules centralized here.
    /// </summary>
    public sealed class AccessGuard : IAccessGuard
    {
        private readonly AppDbContext _db;

        public AccessGuard(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CanUseCoreFeaturesAsync(Guid businessId, CancellationToken ct = default)
        {
            var result = await CheckAsync(businessId, ct);
            return result.Allowed;
        }

        public async Task<AccessCheckResult> CheckAsync(Guid businessId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var sub = await _db.Subscriptions
                .Where(s => s.BusinessId == businessId)
                .OrderByDescending(s => s.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(ct);

            // Case: no subscription at all
            if (sub is null)
            {
                return new AccessCheckResult
                {
                    Allowed = false,
                    Status = null,
                    Message = "You don’t have an active plan. Choose a plan in Billing to start using messaging and campaigns."
                };
            }

            var status = sub.Status;

            switch (status)
            {
                case SubscriptionStatus.Active:
                    return new AccessCheckResult
                    {
                        Allowed = true,
                        Status = status
                    };

                case SubscriptionStatus.Trial:
                    // Trial active
                    if (sub.TrialEndsAtUtc is null || sub.TrialEndsAtUtc > now)
                    {
                        return new AccessCheckResult
                        {
                            Allowed = true,
                            Status = status
                        };
                    }

                    // Trial expired
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = SubscriptionStatus.Expired,
                        Message = "Your trial has ended. Choose a plan in Billing to continue using messaging and campaigns."
                    };

                case SubscriptionStatus.Grace:
                    // Grace = still allowed; show banner via UI if you want
                    return new AccessCheckResult
                    {
                        Allowed = true,
                        Status = status
                    };

                case SubscriptionStatus.PastDue:
                    // Payment failed / due date passed
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = status,
                        Message = "Your subscription payment is overdue. Please clear the pending invoice in Billing to restore access."
                    };

                case SubscriptionStatus.CancelAtPeriodEnd:
                    if (sub.CurrentPeriodEndUtc > now)
                    {
                        // Still in paid period
                        return new AccessCheckResult
                        {
                            Allowed = true,
                            Status = status
                        };
                    }

                    // Period ended -> treat as cancelled
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = SubscriptionStatus.Cancelled,
                        Message = "Your subscription has ended. Select a plan in Billing to continue using your account."
                    };

                case SubscriptionStatus.Cancelled:
                case SubscriptionStatus.Expired:
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = status,
                        Message = "Your subscription is no longer active. Select a plan in Billing to continue using your account."
                    };

                case SubscriptionStatus.Suspended:
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = status,
                        Message = "Your account has been suspended due to payment issues. Update your payment method or contact support to restore access."
                    };

                default:
                    return new AccessCheckResult
                    {
                        Allowed = false,
                        Status = status,
                        Message = "Your subscription status does not allow access. Please check Billing or contact support."
                    };
            }
        }
    }
}
