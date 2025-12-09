#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Payment.DTOs;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Read-only invoice service for now.
    /// Future:
    /// - generation based on subscriptions & usage
    /// - GST-compliant export.
    /// </summary>
    public sealed class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _db;

        public InvoiceService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<InvoiceDto>> GetInvoicesForBusinessAsync(
            Guid businessId,
            CancellationToken ct = default)
        {
            var invoices = await _db.Invoices
                .Include(i => i.LineItems)
                .Where(i => i.BusinessId == businessId)
                .OrderByDescending(i => i.IssuedAtUtc)
                .ToListAsync(ct);

            return invoices.Select(MapToDto).ToList();
        }

        public async Task<InvoiceDto?> GetInvoiceAsync(
            Guid businessId,
            Guid invoiceId,
            CancellationToken ct = default)
        {
            var invoice = await _db.Invoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId, ct);

            return invoice is null ? null : MapToDto(invoice);
        }

        private static InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                BusinessId = invoice.BusinessId,
                SubscriptionId = invoice.SubscriptionId,
                Status = invoice.Status,
                SubtotalAmount = invoice.SubtotalAmount,
                DiscountAmount = invoice.DiscountAmount,
                TaxAmount = invoice.TaxAmount,
                TotalAmount = invoice.TotalAmount,
                Currency = invoice.Currency,
                AppliedCouponCode = invoice.AppliedCouponCode,
                IssuedAtUtc = invoice.IssuedAtUtc,
                DueAtUtc = invoice.DueAtUtc,
                PaidAtUtc = invoice.PaidAtUtc,
                LineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto
                {
                    Id = li.Id,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.LineTotal
                }).ToList()
            };
        }
    }
}
