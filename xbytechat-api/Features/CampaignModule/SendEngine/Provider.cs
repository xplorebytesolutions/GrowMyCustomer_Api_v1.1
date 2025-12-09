namespace xbytechat.api.Features.CampaignModule.SendEngine;

public enum Provider
{
    MetaCloud = 1,
    Pinnacle = 2
}

public static class ProviderUtil
{
    public static Provider Parse(string? s) => s?.Trim().ToUpperInvariant() switch
    {
        "META_CLOUD" or "META" or "METACLOUD" => Provider.MetaCloud,
        "PINNACLE" or "PINBOT" => Provider.Pinnacle,
        _ => Provider.MetaCloud
    };
}
