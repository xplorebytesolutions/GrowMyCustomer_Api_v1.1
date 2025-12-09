#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.Payment.Options;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Razorpay-based implementation of IPaymentGatewayService.
    /// This is a minimal skeleton:
    /// - Creates a Razorpay Order for an existing Invoice.
    /// - Persists a Pending PaymentTransaction.
    /// - Returns data for frontend to open Razorpay Checkout.
    ///
    /// Webhook handling that marks success/failure is done separately.
    /// </summary>
    public sealed class RazorpayPaymentGatewayService : IPaymentGatewayService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http;
        private readonly RazorpayOptions _opts;
        private readonly ILogger<RazorpayPaymentGatewayService> _log;

        public RazorpayPaymentGatewayService(
            AppDbContext db,
            HttpClient httpClient,
            IOptions<RazorpayOptions> opts,
            ILogger<RazorpayPaymentGatewayService> log)
        {
            _db = db;
            _http = httpClient;
            _opts = opts.Value;
            _log = log;

            if (!string.IsNullOrWhiteSpace(_opts.KeyId) &&
                !string.IsNullOrWhiteSpace(_opts.KeySecret))
            {
                var authToken = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_opts.KeyId}:{_opts.KeySecret}"));

                _http.BaseAddress ??= new Uri("https://api.razorpay.com/v1/");
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", authToken);
            }
        }

        /// <summary>
        /// Creates a payment session (Razorpay Order) for the given invoice.
        /// Assumes:
        /// - Invoice is already created with TotalAmount & Currency.
        /// - BusinessId already validated by caller.
        /// </summary>
        public async Task<PaymentSessionResponseDto> CreatePaymentSessionForInvoiceAsync(
            Guid businessId,
            Guid invoiceId,
            string? couponCode,
            CancellationToken ct = default)
        {
            var invoice = await _db.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId, ct);

            if (invoice is null)
                throw new InvalidOperationException("Invoice not found for this business.");

            if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Void)
                throw new InvalidOperationException("Invoice is already closed.");

            if (invoice.TotalAmount <= 0)
                throw new InvalidOperationException("Invoice total must be greater than zero.");

            // ---- Create PaymentTransaction record (Pending) ----
            var tx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                InvoiceId = invoice.Id,
                SubscriptionId = invoice.SubscriptionId,
                Amount = invoice.TotalAmount,
                Currency = invoice.Currency,
                Status = PaymentStatus.Pending,
                Gateway = "Razorpay",
                CreatedAtUtc = DateTime.UtcNow,
                MetaJson = null
            };

            _db.PaymentTransactions.Add(tx);
            await _db.SaveChangesAsync(ct);

            // ---- Call Razorpay Orders API ----
            // amount in paise for INR (multiply by 100)
            var amountInMinor = (int)(invoice.TotalAmount * 100m);

            var payload = new
            {
                amount = amountInMinor,
                currency = invoice.Currency,
                receipt = invoice.InvoiceNumber,
                payment_capture = 1 // auto-capture
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync("orders", content, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error calling Razorpay Orders API");
                throw new InvalidOperationException("Unable to create payment order at this time.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _log.LogError("Razorpay order failed: {Status} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException("Failed to create payment order.");
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var orderId = doc.RootElement.GetProperty("id").GetString();

            // Save order id on transaction
            tx.GatewayOrderId = orderId;
            tx.MetaJson = json;
            await _db.SaveChangesAsync(ct);

            // ---- Return session info for frontend ----
            // Frontend will:
            // - Use Razorpay JS with key_id + order_id + customer info
            // - On success, webhook will confirm and we update DB.
            var session = new PaymentSessionResponseDto
            {
                SessionId = tx.Id.ToString(),
                RedirectUrl = $"{_opts.FrontendBaseUrl}/billing/checkout?orderId={orderId}&txId={tx.Id}"
            };

            return session;
        }
    }
}
