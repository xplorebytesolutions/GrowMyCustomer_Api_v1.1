#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Lightweight read service to power Billing/Insights UI.
    /// Aggregates subscription + invoices + access flag.
    /// </summary>
    public sealed class PaymentOverviewService
    {
        private readonly AppDbContext _db;
        private readonly ISubscriptionService _subscriptions;
        private readonly IInvoiceService _invoices;
        private readonly IAccessGuard _accessGuard;

        public PaymentOverviewService(
            AppDbContext db,
            ISubscriptionService subscriptions,
            IInvoiceService invoices,
            IAccessGuard accessGuard)
        {
            _db = db;
            _subscriptions = subscriptions;
            _invoices = invoices;
            _accessGuard = accessGuard;
        }

        public async Task<PaymentOverviewDto> GetAsync(Guid businessId, CancellationToken ct = default)
        {
            var sub = await _subscriptions.GetCurrentForBusinessAsync(businessId, ct);
            var invoices = await _invoices.GetInvoicesForBusinessAsync(businessId, ct);

            var lastPaid = invoices
                .Where(i => i.Status == InvoiceStatus.Paid)
                .OrderByDescending(i => i.PaidAtUtc ?? i.IssuedAtUtc)
                .FirstOrDefault();

            var canUse = await _accessGuard.CanUseCoreFeaturesAsync(businessId, ct);

            return new PaymentOverviewDto
            {
                CurrentSubscription = sub,
                LastInvoiceAmount = lastPaid?.TotalAmount,
                LastInvoicePaidAtUtc = lastPaid?.PaidAtUtc,
                CurrentPeriodEndUtc = sub?.CurrentPeriodEndUtc,
                CanUseCoreFeatures = canUse,
                RecentInvoices = invoices
                    .Take(10)
                    .ToList()
            };
        }
    }
}
