using System;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Inbox.Services
{
    public interface IAgentAssignmentService
    {
        Task<bool> IsAgentAvailableAsync(Guid businessId);
        Task AssignAgentToContactAsync(Guid businessId, Guid contactId);
    }
}
