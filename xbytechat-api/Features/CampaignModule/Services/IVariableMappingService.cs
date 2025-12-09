using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Loads saved variable mappings for a campaign (e.g., token -> CSV header or "constant:...").
    /// Current project does not require this to complete 2.4; this is a seam for future use.
    /// </summary>
    public interface IVariableMappingService
    {
        /// <returns>
        /// Dictionary mapping variable token -> source (CSV header name or "constant:Value").
        /// Return an empty dictionary when nothing is saved.
        /// </returns>
        Task<Dictionary<string, string>> GetForCampaignAsync(
            Guid businessId,
            Guid campaignId,
            CancellationToken ct = default);
        Task SaveAsync(
           Guid businessId,
           Guid campaignId,
           Dictionary<string, string> mappings,
           CancellationToken ct = default);

    }
}
