#nullable enable
using System;
using System.Collections.Generic;
using xbytechat.api.Features.Payment.Enums;

namespace xbytechat.api.Features.Payment.DTOs
{
    public sealed class PaymentOverviewDto
    {
        public SubscriptionDto? CurrentSubscription { get; set; }

        public decimal? LastInvoiceAmount { get; set; }
        public DateTime? LastInvoicePaidAtUtc { get; set; }

        public decimal? NextInvoiceEstimatedAmount { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }

        public bool CanUseCoreFeatures { get; set; }

        public List<InvoiceDto> RecentInvoices { get; set; } = new();
    }
}
