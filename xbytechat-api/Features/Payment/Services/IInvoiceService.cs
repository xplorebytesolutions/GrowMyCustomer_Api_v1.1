#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Payment.DTOs;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Creates and retrieves invoices for businesses.
    /// Actual tax/coupon/usage logic will be implemented inside.
    /// </summary>
    public interface IInvoiceService
    {
        Task<IReadOnlyList<InvoiceDto>> GetInvoicesForBusinessAsync(
            Guid businessId,
            CancellationToken ct = default);

        Task<InvoiceDto?> GetInvoiceAsync(Guid businessId, Guid invoiceId, CancellationToken ct = default);
    }
}

