using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.Inbox.Services
{
    public class AgentAssignmentService : IAgentAssignmentService
    {
        private readonly ILogger<AgentAssignmentService> _logger;

        public AgentAssignmentService(ILogger<AgentAssignmentService> logger)
        {
            _logger = logger;
        }

        public Task<bool> IsAgentAvailableAsync(Guid businessId)
        {
            // 🔧 TODO: Replace with real logic based on your agent pool
            _logger.LogInformation("Checking if agent is available for business {BusinessId}", businessId);
            return Task.FromResult(true); // Assume always available for MVP
        }

        public Task AssignAgentToContactAsync(Guid businessId, Guid contactId)
        {
            // 🔧 TODO: Save agent-contact assignment to DB or notify a human agent
            _logger.LogInformation("Assigning agent to contact {ContactId} for business {BusinessId}", contactId, businessId);
            return Task.CompletedTask;
        }
    }
}
