#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.PlanManagement.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Handles the "subscribe to plan" flow:
    /// - Validates plan & coupon
    /// - Creates invoice with coupon + GST
    /// - Creates payment session via gateway.
    /// </summary>
    public sealed class SubscriptionCheckoutService : ISubscriptionCheckoutService
    {
        private readonly AppDbContext _db;
        private readonly ICouponService _coupons;
        private readonly IPaymentGatewayService _gateway;

        public SubscriptionCheckoutService(
            AppDbContext db,
            ICouponService coupons,
            IPaymentGatewayService gateway)
        {
            _db = db;
            _coupons = coupons;
            _gateway = gateway;
        }

        public async Task<PaymentSessionResponseDto> StartSubscriptionCheckoutAsync(
            Guid businessId,
            CreateSubscriptionRequestDto request,
            CancellationToken ct = default)
        {
            var plan = await _db.Set<Plan>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
                ?? throw new InvalidOperationException("Plan not found.");

            // Resolve base price from plan & cycle (adjust property names to your Plan model).
            var (price, currency) = GetPlanPrice(plan, request.BillingCycle);
            if (price <= 0)
                throw new InvalidOperationException("Plan price is not configured.");

            var subtotal = price;

            // ---- Apply coupon if present ----
            string? appliedCode = null;
            decimal discountAmount = 0m;

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var coupon = await _coupons.ValidateCouponAsync(request.CouponCode, currency, ct);
                if (coupon != null)
                {
                    // Load full Coupon entity to use in helper
                    var fullCoupon = await _db.Coupons
                        .FirstAsync(c => c.Id == coupon.Id, ct);

                    var (disc, finalAfterDisc) = CouponPricingHelper.ApplyCoupon(fullCoupon, subtotal);
                    discountAmount = disc;
                    subtotal = finalAfterDisc;
                    appliedCode = coupon.Code;
                }
            }

            // ---- Apply GST (India) ----
            // For now assume intra-state = false/true via a simple flag or config.
            // You can later compute based on Business address vs your GST registration.
            var (taxAmount, taxBreakdownJson) =
                TaxCalculator.CalculateGstForIndianCustomer(subtotal, isInterState: true);

            var total = subtotal + taxAmount;

            // ---- Create Invoice ----
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                SubscriptionId = null,                // will be linked on success
                PlanId = plan.Id,
                BillingCycle = request.BillingCycle,
                InvoiceNumber = GenerateInvoiceNumber(),
                Status = InvoiceStatus.Open,
                SubtotalAmount = price,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                TotalAmount = total,
                Currency = currency,
                AppliedCouponCode = appliedCode,
                TaxBreakdownJson = taxBreakdownJson,
                IssuedAtUtc = DateTime.UtcNow,
                DueAtUtc = null
            };

            // Single line item: "Plan - cycle"
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                Description = $"{plan.Name} - {request.BillingCycle}",
                Quantity = 1,
                UnitPrice = price,
                LineTotal = price
            });

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            // ---- Create payment session via gateway ----
            var session = await _gateway.CreatePaymentSessionForInvoiceAsync(
                businessId,
                invoice.Id,
                appliedCode,
                ct);

            return session;
        }

        private static (decimal price, string currency) GetPlanPrice(Plan plan, BillingCycle cycle)
        {
            // We don't hardcode your Plan schema here.
            // Instead, we try common property names via reflection so this compiles
            // even if your Plan model is different. Later you can replace this
            // with a strict implementation once pricing fields are finalized.

            var type = plan.GetType();

            // 1) Currency: try common names, fallback to INR
            var currencyProp =
                type.GetProperty("Currency") ??
                type.GetProperty("DefaultCurrency") ??
                type.GetProperty("PlanCurrency");

            var currency =
                (currencyProp?.GetValue(plan) as string)
                ?? "INR";

            // 2) Price: prefer cycle-specific fields if they exist
            string[] yearlyNames = { "YearlyPrice", "PriceYearly", "YearlyAmount", "AnnualPrice" };
            string[] monthlyNames = { "MonthlyPrice", "PriceMonthly", "MonthlyAmount" };

            decimal price = 0m;

            if (cycle == BillingCycle.Yearly)
            {
                foreach (var name in yearlyNames)
                {
                    var p = type.GetProperty(name);
                    if (p != null && p.PropertyType == typeof(decimal))
                    {
                        price = (decimal)(p.GetValue(plan) ?? 0m);
                        if (price > 0) break;
                    }
                }
            }
            else
            {
                foreach (var name in monthlyNames)
                {
                    var p = type.GetProperty(name);
                    if (p != null && p.PropertyType == typeof(decimal))
                    {
                        price = (decimal)(p.GetValue(plan) ?? 0m);
                        if (price > 0) break;
                    }
                }
            }

            // 3) Fallback: single generic price property if cycle-specific not found
            if (price <= 0)
            {
                var singlePriceProp =
                    type.GetProperty("Price") ??
                    type.GetProperty("Amount") ??
                    type.GetProperty("BasePrice");

                if (singlePriceProp != null && singlePriceProp.PropertyType == typeof(decimal))
                {
                    price = (decimal)(singlePriceProp.GetValue(plan) ?? 0m);
                }
            }

            // At this point:
            // - If price is still 0 => your Plan doesn't have any of the expected fields.
            //   That’s a config/contract decision, not a runtime crash.
            return (price, currency);
        }

        private static string GenerateInvoiceNumber()
        {
            // Primitive; replace with your own sequence if needed.
            return "XP-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        }
    }
}
