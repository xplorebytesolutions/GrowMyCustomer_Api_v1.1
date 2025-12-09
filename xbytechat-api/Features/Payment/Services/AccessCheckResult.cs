#nullable enable
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Result of an access check for core, billable features.
    /// </summary>
    public sealed class AccessCheckResult
    {
        public bool Allowed { get; init; }
        public SubscriptionStatus? Status { get; init; }
        public string? Message { get; init; }
    }
}
