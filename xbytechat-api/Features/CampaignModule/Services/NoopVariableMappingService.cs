using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Default stub: returns no saved mappings.
    /// Swap this out later with a DB-backed implementation that reads CampaignVariableMap.
    /// </summary>
    public sealed class NoopVariableMappingService : IVariableMappingService
    {
        public Task<Dictionary<string, string>> GetForCampaignAsync(
            Guid businessId,
            Guid campaignId,
            CancellationToken ct = default)
        {
            return Task.FromResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        public Task SaveAsync(
           Guid businessId,
           Guid campaignId,
           Dictionary<string, string> mappings,
           CancellationToken ct = default)
        {
            // no-op
            return Task.CompletedTask;
        }

    }
}
