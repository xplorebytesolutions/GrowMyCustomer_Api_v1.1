#nullable enable
using System;
using System.Collections.Generic;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.Payment.Models
{
    /// <summary>
    /// Represents a bill issued to a business for a period, plan, or usage.
    /// Can be mapped to one or more payment transactions.
    /// </summary>
    public class Invoice
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Business this invoice belongs to.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Optional related subscription for recurring charges.
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// Human-readable invoice number (e.g. "XP-2025-000123").
        /// </summary>
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the invoice (Draft/Open/Paid/Void).
        /// </summary>
        public InvoiceStatus Status { get; set; }

        /// <summary>
        /// Total before tax.
        /// </summary>
        public decimal SubtotalAmount { get; set; }

        /// <summary>
        /// Total tax amount (GST/VAT etc.) if applicable.
        /// </summary>
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Grand total (Subtotal + Tax - Discounts).
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// ISO currency code, e.g. "INR".
        /// </summary>
        public string Currency { get; set; } = "INR";

        /// <summary>
        /// When invoice was issued (UTC).
        /// </summary>
        public DateTime IssuedAtUtc { get; set; }

        /// <summary>
        /// When payment is due (UTC).
        /// </summary>
        public DateTime? DueAtUtc { get; set; }

        /// <summary>
        /// When invoice was fully paid (UTC).
        /// </summary>
        public DateTime? PaidAtUtc { get; set; }

        /// <summary>
        /// Optional free-form notes (e.g., terms, adjustments).
        /// </summary>
        public string? Notes { get; set; }

        public string? AppliedCouponCode { get; set; }

        /// <summary>
        /// Total discount amount applied on this invoice (>= 0).
        /// </summary>
        public decimal DiscountAmount { get; set; }

        public string? TaxBreakdownJson { get; set; }


        /// <summary>
        /// Optional: plan this invoice refers to (for subscription charges).
        /// </summary>
        public Guid? PlanId { get; set; }

        /// <summary>
        /// Optional: billing cycle for this invoice's subscription charge.
        /// </summary>
        public BillingCycle? BillingCycle { get; set; }
        // ---- Navigation ----

        public Business? Business { get; set; }
        public Subscription? Subscription { get; set; }

        public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();

        public Plan? Plan { get; set; }
    }
}
