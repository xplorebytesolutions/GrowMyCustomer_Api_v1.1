#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Payment.DTOs;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Abstraction over the underlying payment provider (Razorpay, Stripe, etc.).
    /// Implementation will:
    /// - create checkout/payment sessions
    /// - verify signatures
    /// - map webhooks to PaymentTransaction updates.
    /// </summary>
    public interface IPaymentGatewayService
    {
        Task<PaymentSessionResponseDto> CreatePaymentSessionForInvoiceAsync(
            Guid businessId,
            Guid invoiceId,
            string? couponCode,
            CancellationToken ct = default);

        // We will add webhook-handling signatures later when wiring gateway.
    }
}

