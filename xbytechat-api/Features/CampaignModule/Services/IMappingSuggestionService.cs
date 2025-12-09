using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Suggests a token->source mapping ("csv:Header" or "static:") given a campaign and CSV batch.
    /// Uses Campaign.TemplateParameters if present; otherwise derives tokens from CSV headers.
    /// </summary>
    public interface IMappingSuggestionService
    {
        Task<Dictionary<string, string>> SuggestAsync(
            Guid businessId,
            Guid campaignId,
            Guid batchId,
            CancellationToken ct = default);
    }
}
