#nullable enable

namespace xbytechat.api.Features.Payment.DTOs
{
    /// <summary>
    /// Response containing info needed by the frontend to redirect the user
    /// to the payment gateway (hosted page, popup, etc.).
    /// </summary>
    public class PaymentSessionResponseDto
    {
        /// <summary>
        /// Internal id for tracking this session/intent.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// URL to redirect the user to (Razorpay Checkout, Stripe Checkout, etc.).
        /// </summary>
        public string RedirectUrl { get; set; } = string.Empty;
    }
}

