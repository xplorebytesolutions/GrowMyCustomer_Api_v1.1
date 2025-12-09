using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.AccountInsights.DTOs;
using xbytechat.api.Features.AccountInsights.Models;

namespace xbytechat.api.Features.AccountInsights.Services
{
    public interface IAccountInsightsService
    {
        Task<AccountInsightsSnapshotDto> GetSnapshotAsync(Guid businessId);

        Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetSnapshotsAsync(
            int page = 1,
            int pageSize = 50,
            Guid? partnerId = null);

        Task<AccountInsightsSummaryDto> GetSummaryAsync(Guid? partnerId = null);

        Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetTrialsExpiringSoonAsync(int days = 3);

        Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetByLifecycleStageAsync(
            AccountLifecycleStage stage,
            Guid? partnerId = null,
            int page = 1,
            int pageSize = 100);

        Task<IReadOnlyList<AccountInsightsActionDto>> GetRecentActionsAsync(
       Guid businessId,
       int limit = 10);

        Task LogActionAsync(
           Guid businessId, string actionType,string label, string actor,string metaJson = null);
    }
}
