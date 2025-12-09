using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.Auditing.FlowExecutions.DTOs;

namespace xbytechat.api.Features.Auditing.FlowExecutions.Services
{
    /// <summary>
    /// Read-only query service for inspecting flow execution logs.
    /// Used by internal tools / analytics / debug endpoints.
    /// </summary>
    public interface IFlowExecutionQueryService
    {
        /// <summary>
        /// Returns recent flow execution steps for a given business,
        /// ordered by ExecutedAtUtc descending.
        /// </summary>
        /// <param name="businessId">The tenant/business id to filter by (required).</param>
        /// <param name="filter">Optional filters for origin, flow, contact, and limit.</param>
        Task<IReadOnlyList<FlowExecutionLogDto>> GetRecentExecutionsAsync(
            Guid businessId,
            FlowExecutionFilter filter,
            CancellationToken ct = default);

        Task<IReadOnlyList<FlowExecutionLogDto>> GetRunTimelineAsync(
           Guid businessId,
           Guid runId,
           CancellationToken ct = default);
    }
}
