using System;
using System.Threading.Tasks;
using xbytechat_api.Features.Billing.DTOs;

namespace xbytechat_api.Features.Billing.Services
{
    public interface IBillingIngestService
    {
        Task IngestFromSendResponseAsync(Guid businessId, Guid messageLogId, string provider, string rawResponseJson);
        Task IngestFromWebhookAsync(Guid businessId, string provider, string payloadJson);
        
    }
}
