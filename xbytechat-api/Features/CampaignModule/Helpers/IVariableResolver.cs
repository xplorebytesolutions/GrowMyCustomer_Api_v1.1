using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.Helpers
{
    public interface IVariableResolver
    {
        Dictionary<string, string> ResolveVariables(
            IReadOnlyDictionary<string, string> rowData,
            IReadOnlyDictionary<string, string>? mappings);
    }
}
