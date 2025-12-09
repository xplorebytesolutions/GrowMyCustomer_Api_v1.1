using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    public interface IAutoReplyLogService
    {
        /// <summary>
        /// Returns the most recent auto-reply triggers for a business,
        /// ordered by TriggeredAt desc.
        /// </summary>
        Task<IReadOnlyList<AutoReplyLogSummaryDto>> GetRecentAsync(
            Guid businessId,
            int take,
            CancellationToken cancellationToken = default);
    }
}
