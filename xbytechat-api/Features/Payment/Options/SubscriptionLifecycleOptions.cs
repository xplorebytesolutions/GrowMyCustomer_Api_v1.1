#nullable enable

namespace xbytechat.api.Features.Payment.Options
{
    /// <summary>
    /// Configuration for automatic subscription status transitions.
    /// Values are in days unless noted.
    /// </summary>
    public sealed class SubscriptionLifecycleOptions
    {
        /// <summary>
        /// After this many days in PastDue, move to Suspended.
        /// </summary>
        public int PastDueToSuspendedDays { get; set; } = 7;
    }
}
