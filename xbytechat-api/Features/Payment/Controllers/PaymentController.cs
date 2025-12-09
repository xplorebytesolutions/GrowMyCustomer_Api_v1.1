#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Services;
using xbytechat.api.Shared; // assuming User.GetBusinessId() lives here or similar

namespace xbytechat.api.Features.Payment.Controllers
{
    [ApiController]
    [Route("api/payment")]
    [Authorize] // must be authenticated
    public sealed class PaymentController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptions;
        private readonly IInvoiceService _invoices;
        private readonly ICouponService _coupons;
        private readonly IPaymentGatewayService _gateway;
        private readonly ISubscriptionCheckoutService _checkout;
        private readonly PaymentOverviewService _overview;
        public PaymentController(
            ISubscriptionService subscriptions,
            IInvoiceService invoices,
            ICouponService coupons,
            IPaymentGatewayService gateway,
            ISubscriptionCheckoutService checkout,
            PaymentOverviewService overview)
        {
            _subscriptions = subscriptions;
            _invoices = invoices;
            _coupons = coupons;
            _gateway = gateway;
            _checkout = checkout;
            _overview = overview;
        }

        /// <summary>
        /// Returns the current subscription for the logged-in business.
        /// </summary>
        [HttpGet("subscription")]
        public async Task<IActionResult> GetCurrentSubscription(CancellationToken ct)
        {
            var businessId = User.GetBusinessId(); // your existing extension
            var sub = await _subscriptions.GetCurrentForBusinessAsync(businessId, ct);
            return Ok(new { ok = true, data = sub });
        }

        /// <summary>
        /// Creates or updates subscription for the logged-in business.
        /// NOTE:
        /// - For MVP this directly activates.
        /// - Later, call only after payment confirmation.
        /// </summary>
        [HttpPost("subscription")]
        public async Task<IActionResult> CreateOrUpdateSubscription(
            [FromBody] CreateSubscriptionRequestDto request,
            CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var sub = await _subscriptions.CreateOrUpdateSubscriptionAsync(businessId, request, ct);
            return Ok(new { ok = true, data = sub });
        }

        /// <summary>
        /// Marks the current subscription to cancel at period end.
        /// </summary>
        [HttpPost("subscription/cancel-at-period-end")]
        public async Task<IActionResult> CancelAtPeriodEnd(CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var ok = await _subscriptions.MarkCancelAtPeriodEndAsync(businessId, ct);
            return Ok(new { ok });
        }

        /// <summary>
        /// Reactivates auto-renew for the current subscription.
        /// </summary>
        [HttpPost("subscription/reactivate")]
        public async Task<IActionResult> Reactivate(CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var ok = await _subscriptions.ReactivateAutoRenewAsync(businessId, ct);
            return Ok(new { ok });
        }

        /// <summary>
        /// Returns all invoices for the logged-in business.
        /// </summary>
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices(CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var items = await _invoices.GetInvoicesForBusinessAsync(businessId, ct);
            return Ok(new { ok = true, data = items });
        }

        /// <summary>
        /// Returns a specific invoice.
        /// </summary>
        [HttpGet("invoices/{invoiceId:guid}")]
        public async Task<IActionResult> GetInvoice(Guid invoiceId, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var invoice = await _invoices.GetInvoiceAsync(businessId, invoiceId, ct);
            if (invoice is null)
                return NotFound(new { ok = false, message = "Invoice not found." });

            return Ok(new { ok = true, data = invoice });
        }

        /// <summary>
        /// Validates a coupon code for the current context (e.g. before checkout).
        /// </summary>
        [HttpGet("coupon/validate")]
        public async Task<IActionResult> ValidateCoupon(
            [FromQuery] string code,
            [FromQuery] string currency = "INR",
            CancellationToken ct = default)
        {
            var coupon = await _coupons.ValidateCouponAsync(code, currency, ct);
            if (coupon is null)
                return Ok(new { ok = false, message = "Invalid or expired coupon." });

            return Ok(new { ok = true, data = coupon });
        }

        /// <summary>
        /// Creates a payment session for the given invoice and returns redirect info.
        /// Frontend should call this before opening Razorpay checkout.
        /// </summary>
        [HttpPost("invoices/{invoiceId:guid}/checkout")]
        public async Task<IActionResult> CreateCheckoutForInvoice(
            Guid invoiceId,
            [FromBody] CreatePaymentSessionRequestDto body,
            CancellationToken ct)
        {
            var businessId = User.GetBusinessId();

            if (body.InvoiceId.HasValue && body.InvoiceId.Value != invoiceId)
            {
                return BadRequest(new { ok = false, message = "Invoice id mismatch." });
            }

            var session = await _gateway.CreatePaymentSessionForInvoiceAsync(
                businessId,
                invoiceId,
                body.CouponCode,
                ct);

            return Ok(new { ok = true, data = session });
        }
        /// <summary>
        /// Starts subscription checkout for a selected plan:
        /// - Creates invoice with coupon+GST
        /// - Creates Razorpay order
        /// - Returns session/redirect info for frontend.
        /// </summary>
        [HttpPost("subscribe/checkout")]
        public async Task<IActionResult> StartSubscriptionCheckout(
            [FromBody] CreateSubscriptionRequestDto request,
            CancellationToken ct)
        {
            var businessId = User.GetBusinessId();

            var session = await _checkout.StartSubscriptionCheckoutAsync(
                businessId,
                request,
                ct);

            return Ok(new { ok = true, data = session });
        }
        /// <summary>
        /// Returns a compact payment overview for the logged-in business.
        /// Intended for Billing page + Account Insights integration.
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            var data = await _overview.GetAsync(businessId, ct);
            return Ok(new { ok = true, data });
        }

    }
}
