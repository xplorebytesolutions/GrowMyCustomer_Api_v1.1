using System;
using System.Threading.Tasks;
using xbytechat.api.Features.FlowAnalytics.DTOs;

namespace xbytechat.api.Features.FlowAnalytics.Services
{
    public interface IFlowAnalyticsService
    {
        Task<FlowAnalyticsSummaryDto> GetAnalyticsSummaryAsync(Guid businessId);
        Task<List<MostTriggeredStepDto>> GetMostTriggeredStepsAsync(Guid businessId);
        Task<List<FlowAnalyticsStepJourneyDto>> GetStepJourneyBreakdownAsync(Guid businessId, DateTime? startDate, DateTime? endDate);

    }
}
