#nullable enable
namespace xbytechat.api.Features.Payment.Options
{
    /// <summary>
    /// Razorpay configuration bound from appsettings.
    /// Never hard-code secrets in code.
    /// </summary>
    public sealed class RazorpayOptions
    {
        public string KeyId { get; set; } = string.Empty;
        public string KeySecret { get; set; } = string.Empty;

        /// <summary>
        /// Secret for validating Razorpay webhooks.
        /// </summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// Base URL of your frontend where you handle success/failure (if needed).
        /// </summary>
        public string FrontendBaseUrl { get; set; } = string.Empty;
    }
}
