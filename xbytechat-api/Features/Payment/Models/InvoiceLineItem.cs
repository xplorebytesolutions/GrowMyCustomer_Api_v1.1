#nullable enable
using System;

namespace xbytechat.api.Features.Payment.Models
{
    /// <summary>
    /// Represents a single line item on an invoice
    /// (plan fee, add-on, usage charge, discount as negative, etc.).
    /// </summary>
    public class InvoiceLineItem
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Parent invoice.
        /// </summary>
        public Guid InvoiceId { get; set; }

        /// <summary>
        /// Short description shown on the invoice.
        /// e.g. "Pro Plan - Monthly", "WhatsApp Usage - Jan 2025"
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Logical quantity (months, message blocks, add-ons).
        /// </summary>
        public decimal Quantity { get; set; } = 1m;

        /// <summary>
        /// Price per unit (before tax).
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Computed total for this line (Quantity * UnitPrice).
        /// </summary>
        public decimal LineTotal { get; set; }

        /// <summary>
        /// Optional metadata (JSON) for internal reconciliation.
        /// </summary>
        public string? MetaJson { get; set; }

        // ---- Navigation ----

        public Invoice? Invoice { get; set; }
    }
}
