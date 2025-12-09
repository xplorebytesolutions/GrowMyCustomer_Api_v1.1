using System;
using System.Threading.Tasks;
using xbytechat_api.Features.Billing.DTOs;

namespace xbytechat_api.Features.Billing.Services
{
    public interface IBillingReadService
    {
        Task<BillingSnapshotDto> GetBusinessBillingSnapshotAsync(Guid businessId, DateOnly from, DateOnly to);
    }
}
