namespace xbytechat.api.Features.Payment.Enums
{
    /// <summary>
    /// Represents the lifecycle state of a business account's subscription.
    /// This will drive access control and account insights.
    /// </summary>
    public enum SubscriptionStatus
    {
        /// <summary>
        /// Newly created trial, within the active trial window.
        /// </summary>
        Trial = 0,

        /// <summary>
        /// Active and fully paid. All subscribed features are enabled.
        /// </summary>
        Active = 1,

        /// <summary>
        /// Payment failed or not received; retry / dunning in progress.
        /// </summary>
        PastDue = 2,

        /// <summary>
        /// In grace window before hard suspension.
        /// </summary>
        Grace = 3,

        /// <summary>
        /// Marked to cancel at the end of the current billing period.
        /// </summary>
        CancelAtPeriodEnd = 4,

        /// <summary>
        /// Fully cancelled with no future renewals.
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// Access blocked due to non-payment / policy violation.
        /// </summary>
        Suspended = 6,

        /// <summary>
        /// Trial ended without activation.
        /// </summary>
        Expired = 7
    }
}
