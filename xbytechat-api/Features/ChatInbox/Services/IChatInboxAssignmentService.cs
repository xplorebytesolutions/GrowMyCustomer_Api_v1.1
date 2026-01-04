using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ChatInbox.DTOs;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public interface IChatInboxAssignmentService
    {
        Task<List<AgentDto>> GetAgentsAsync(Guid businessId, CancellationToken ct = default);

        Task AssignAsync(Guid businessId, Guid contactId, Guid userId, Guid actorUserId, CancellationToken ct = default);
        Task UnassignAsync(Guid businessId, Guid contactId, Guid actorUserId, CancellationToken ct = default);
        Task SetStatusAsync(Guid businessId, Guid contactId, string status, Guid actorUserId, CancellationToken ct = default);
    }
}

