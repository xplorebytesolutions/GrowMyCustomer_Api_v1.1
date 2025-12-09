#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.Payment.Options;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Runs periodic maintenance to keep subscription statuses in sync with time & invoices.
    /// Should be called by a scheduled job (e.g. daily).
    /// </summary>
    public sealed class SubscriptionLifecycleService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SubscriptionLifecycleService> _log;
        private readonly SubscriptionLifecycleOptions _options;

        public SubscriptionLifecycleService(
            AppDbContext db,
            IOptions<SubscriptionLifecycleOptions> options,
            ILogger<SubscriptionLifecycleService> log)
        {
            _db = db;
            _log = log;
            _options = options.Value;
        }

        /// <summary>
        /// Run lifecycle sync for all subscriptions.
        /// Safe to run daily (or more often).
        /// </summary>
        public async Task RunAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var pastDueCutoff = now.AddDays(-_options.PastDueToSuspendedDays);

            // Load subs in one go; for huge scale you'd batch.
            var subs = await _db.Subscriptions
                .AsTracking()
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                try
                {
                    switch (sub.Status)
                    {
                        case SubscriptionStatus.Trial:
                            if (sub.TrialEndsAtUtc is not null &&
                                sub.TrialEndsAtUtc.Value < now)
                            {
                                sub.Status = SubscriptionStatus.Expired;
                            }
                            break;

                        case SubscriptionStatus.CancelAtPeriodEnd:
                            if (sub.CurrentPeriodEndUtc < now)
                            {
                                sub.Status = SubscriptionStatus.Cancelled;
                            }
                            break;

                        case SubscriptionStatus.PastDue:
                            // If there is an unpaid invoice older than cutoff,
                            // we suspend the subscription.
                            var hasOldUnpaid = await _db.Invoices
                                .AnyAsync(i =>
                                    i.SubscriptionId == sub.Id &&
                                    i.Status != InvoiceStatus.Paid &&
                                    (i.DueAtUtc ?? i.IssuedAtUtc) < pastDueCutoff,
                                    ct);

                            if (hasOldUnpaid)
                            {
                                sub.Status = SubscriptionStatus.Suspended;
                            }
                            break;

                        default:
                            // Active, Grace, Suspended, Cancelled, Expired:
                            // no automatic change here for now.
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex,
                        "Error processing lifecycle for subscription {SubscriptionId}", sub.Id);
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
