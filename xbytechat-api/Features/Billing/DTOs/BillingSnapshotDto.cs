using System.Collections.Generic;

namespace xbytechat_api.Features.Billing.DTOs
{
    public class BillingSnapshotDto
    {
        public int TotalMessages { get; set; }
        public int ChargeableMessages { get; set; }
        public int FreeMessages { get; set; }
        public Dictionary<string, int> CountByCategory { get; set; } = new();    // marketing, utility, authentication, service, free_entry
        public Dictionary<string, decimal> SpendByCurrency { get; set; } = new();// "USD" => 12.34, "INR" => 250.00
    }
}
