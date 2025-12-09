using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.PlanManagement.Services
{
    public interface IPlanManager
    {
        /// <summary>
        /// Checks if business has enough quota to send a message.
        /// </summary>
        Task<ResponseResult> CheckQuotaBeforeSendingAsync(Guid businessId);
        Dictionary<string, bool> GetPlanFeatureMap(string plan);
    }
}
