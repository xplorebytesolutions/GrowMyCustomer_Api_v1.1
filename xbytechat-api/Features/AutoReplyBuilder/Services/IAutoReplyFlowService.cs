using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    public interface IAutoReplyFlowService
    {
        Task<IReadOnlyList<AutoReplyFlowSummaryDto>> GetFlowsForBusinessAsync(Guid businessId, CancellationToken ct = default);

        Task<AutoReplyFlowDto?> GetFlowAsync(Guid businessId, Guid flowId, CancellationToken ct = default);

        Task<AutoReplyFlowDto> SaveFlowAsync(Guid businessId, AutoReplyFlowDto dto, CancellationToken ct = default);

        Task DeleteFlowAsync(Guid businessId, Guid flowId, CancellationToken ct = default);
        Task SetActiveAsync(
            Guid businessId,
            Guid flowId,
            bool isActive,
            CancellationToken ct = default);
    }
}
