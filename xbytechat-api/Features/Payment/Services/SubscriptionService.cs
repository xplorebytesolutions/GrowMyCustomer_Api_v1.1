#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.Payment.Services;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Default implementation for managing business subscriptions.
    /// NOTE:
    /// - For now we assume "backend-approved" calls (e.g. after payment success or admin action).
    /// - Later we will tighten this to be driven purely by gateway webhooks.
    /// </summary>
    public sealed class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SubscriptionService> _log;

        public SubscriptionService(AppDbContext db, ILogger<SubscriptionService> log)
        {
            _db = db;
            _log = log;
        }

        /// <summary>
        /// Gets the latest subscription for a business (if any).
        /// </summary>
        public async Task<SubscriptionDto?> GetCurrentForBusinessAsync(
            Guid businessId,
            CancellationToken ct = default)
        {
            var sub = await _db.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.BusinessId == businessId)
                .OrderByDescending(s => s.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(ct);

            if (sub is null) return null;

            return MapToDto(sub);
        }

        /// <summary>
        /// Creates or updates a subscription for a business.
        /// IMPORTANT:
        /// - This is a logical operation; real activation should be tied to payment success later.
        /// - For MVP we mark as Active immediately (assuming external validation).
        /// </summary>
        public async Task<SubscriptionDto> CreateOrUpdateSubscriptionAsync(
            Guid businessId,
            CreateSubscriptionRequestDto request,
            CancellationToken ct = default)
        {
            var business = await _db.Set<Business>()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == businessId, ct)
                ?? throw new InvalidOperationException("Business not found.");

            var plan = await _db.Set<Plan>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
                ?? throw new InvalidOperationException("Plan not found.");

            var now = DateTime.UtcNow;

            // Simple: 1 active subscription per business (latest wins).
            var existing = await _db.Subscriptions
                .Where(s => s.BusinessId == businessId
                            && s.Status != SubscriptionStatus.Cancelled
                            && s.Status != SubscriptionStatus.Expired)
                .OrderByDescending(s => s.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(ct);

            var periodEnd = request.BillingCycle switch
            {
                BillingCycle.Yearly => now.AddYears(1),
                _ => now.AddMonths(1)
            };

            if (existing is null)
            {
                var sub = new Subscription
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    PlanId = plan.Id,
                    Status = SubscriptionStatus.Active,   // later: controlled by payment result
                    BillingCycle = request.BillingCycle,
                    CurrentPeriodStartUtc = now,
                    CurrentPeriodEndUtc = periodEnd,
                    AutoRenew = true,
                    CancelAtPeriodEnd = false,
                    TrialEndsAtUtc = null
                };

                _db.Subscriptions.Add(sub);
                await _db.SaveChangesAsync(ct);

                return MapToDto(sub, plan.Name);
            }
            else
            {
                // Update existing into new plan / cycle.
                existing.PlanId = plan.Id;
                existing.BillingCycle = request.BillingCycle;
                existing.Status = SubscriptionStatus.Active;
                existing.AutoRenew = true;
                existing.CancelAtPeriodEnd = false;
                existing.CurrentPeriodStartUtc = now;
                existing.CurrentPeriodEndUtc = periodEnd;

                await _db.SaveChangesAsync(ct);

                return MapToDto(existing, plan.Name);
            }
        }

        public async Task<bool> MarkCancelAtPeriodEndAsync(
            Guid businessId,
            CancellationToken ct = default)
        {
            var sub = await _db.Subscriptions
                .Where(s => s.BusinessId == businessId &&
                            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial))
                .OrderByDescending(s => s.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(ct);

            if (sub is null) return false;

            sub.CancelAtPeriodEnd = true;
            sub.Status = SubscriptionStatus.CancelAtPeriodEnd;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ReactivateAutoRenewAsync(
            Guid businessId,
            CancellationToken ct = default)
        {
            var sub = await _db.Subscriptions
                .Where(s => s.BusinessId == businessId &&
                            (s.Status == SubscriptionStatus.CancelAtPeriodEnd ||
                             s.Status == SubscriptionStatus.Active))
                .OrderByDescending(s => s.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(ct);

            if (sub is null) return false;

            sub.CancelAtPeriodEnd = false;
            if (sub.Status == SubscriptionStatus.CancelAtPeriodEnd)
            {
                sub.Status = SubscriptionStatus.Active;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }

        private static SubscriptionDto MapToDto(Subscription sub, string? planNameOverride = null)
        {
            return new SubscriptionDto
            {
                Id = sub.Id,
                BusinessId = sub.BusinessId,
                PlanId = sub.PlanId,
                PlanName = planNameOverride ?? sub.Plan?.Name ?? string.Empty,
                Status = sub.Status,
                BillingCycle = sub.BillingCycle,
                CurrentPeriodStartUtc = sub.CurrentPeriodStartUtc,
                CurrentPeriodEndUtc = sub.CurrentPeriodEndUtc,
                TrialEndsAtUtc = sub.TrialEndsAtUtc,
                AutoRenew = sub.AutoRenew,
                CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
                GatewayCustomerId = sub.GatewayCustomerId,
                GatewaySubscriptionId = sub.GatewaySubscriptionId
            };
        }
    }
}
