#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.Payment.Options;
using xbytechat.api.Features.Payment.Services;

namespace xbytechat.api.Features.Payment.Controllers
{
    [ApiController]
    [Route("api/payment/webhook/razorpay")]
    public sealed class PaymentWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly RazorpayOptions _opts;
        private readonly ISubscriptionService _subscriptions;

        public PaymentWebhookController(
            AppDbContext db,
            IOptions<RazorpayOptions> opts,
            ISubscriptionService subscriptions)
        {
            _db = db;
            _opts = opts.Value;
            _subscriptions = subscriptions;
        }

        [HttpPost]
        public async Task<IActionResult> Handle(CancellationToken ct)
        {
            // 1. Read body
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(ct);
            }

            // 2. Verify signature
            var signature = Request.Headers["X-Razorpay-Signature"].ToString();
            if (!VerifySignature(body, signature, _opts.WebhookSecret))
            {
                return Unauthorized();
            }

            // 3. Parse minimal fields
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Webhook schema differs by event; we read what we need:
            // payment.captured / payment.failed etc.
            var eventType = root.GetProperty("event").GetString();

            if (string.IsNullOrWhiteSpace(eventType))
                return Ok(); // ignore unknown

            if (eventType.StartsWith("payment."))
            {
                var payload = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
                var paymentId = payload.GetProperty("id").GetString();
                var orderId = payload.TryGetProperty("order_id", out var oidEl) ? oidEl.GetString() : null;
                var status = payload.GetProperty("status").GetString();

                if (!string.IsNullOrWhiteSpace(orderId))
                {
                    var tx = await _db.PaymentTransactions
                        .FirstOrDefaultAsync(t => t.GatewayOrderId == orderId, ct);

                    if (tx != null)
                    {
                        await ApplyPaymentStatusAsync(tx, paymentId, status, ct);
                    }
                }
            }

            // You may also handle order.paid events similarly.

            return Ok();
        }

        private static bool VerifySignature(string body, string signature, string secret)
        {
            if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
                return false;

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(bodyBytes);
            var generated = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            return string.Equals(generated, signature, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ApplyPaymentStatusAsync(
            PaymentTransaction tx,
            string? paymentId,
            string? paymentStatus,
            CancellationToken ct)
        {
            // Normalize
            paymentStatus = paymentStatus?.ToLowerInvariant();

            if (paymentStatus == "captured" || paymentStatus == "authorized" || paymentStatus == "paid")
            {
                // ---- SUCCESS ----
                tx.Status = PaymentStatus.Success;
                tx.GatewayPaymentId = paymentId;
                tx.CompletedAtUtc = DateTime.UtcNow;

                var invoice = await _db.Invoices
                    .Include(i => i.Plan)
                    .FirstOrDefaultAsync(i => i.Id == tx.InvoiceId, ct);

                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.Paid;
                    invoice.PaidAtUtc = DateTime.UtcNow;
                }

                // If this invoice is for a subscription (has PlanId + BillingCycle),
                // we activate/update subscription via SubscriptionService.
                if (invoice?.PlanId != null && invoice.BillingCycle != null)
                {
                    await _subscriptions.CreateOrUpdateSubscriptionAsync(
                        tx.BusinessId,
                        new DTOs.CreateSubscriptionRequestDto
                        {
                            PlanId = invoice.PlanId.Value,
                            BillingCycle = invoice.BillingCycle.Value,
                            // Coupon already baked into invoice; no need to send here.
                            CouponCode = null
                        },
                        ct);
                }
            }
            else if (paymentStatus == "failed")
            {
                // ---- FAILURE ----
                tx.Status = PaymentStatus.Failed;
                tx.GatewayPaymentId = paymentId;
                tx.CompletedAtUtc = DateTime.UtcNow;

                var invoice = await _db.Invoices
                    .FirstOrDefaultAsync(i => i.Id == tx.InvoiceId, ct);

                if (invoice != null)
                {
                    // Keep invoice open for retry / dunning if it was draft.
                    if (invoice.Status == InvoiceStatus.Draft)
                    {
                        invoice.Status = InvoiceStatus.Open;
                    }

                    // If this was a renewal / subscription-linked invoice,
                    // mark subscription as PastDue so AccessGuard can block.
                    if (invoice.SubscriptionId != null)
                    {
                        var sub = await _db.Subscriptions
                            .FirstOrDefaultAsync(s => s.Id == invoice.SubscriptionId.Value, ct);

                        if (sub != null &&
                            (sub.Status == SubscriptionStatus.Active ||
                             sub.Status == SubscriptionStatus.Grace))
                        {
                            sub.Status = SubscriptionStatus.PastDue;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
        }


    }
}
