namespace xbytechat.api.Features.CampaignModule.Services.SendPipeline
{
    public static class ProviderNormalizer
    {

        public static string ForDb(string? providerFromSettings)
        {
            var v = (providerFromSettings ?? string.Empty).Trim();
            // If you store "meta_cloud" / "pinnacle" in DB, keep it as-is:
            return v.ToLowerInvariant(); // e.g. "meta_cloud", "pinnacle"
        }

        public static string ForSend(string? providerFromSettings)
        {
            var v = (providerFromSettings ?? string.Empty).Trim().ToLowerInvariant();
            return v switch
            {
                "pinnacle" => "PINNACLE",
                "meta_cloud" => "META_CLOUD",
                _ => providerFromSettings ?? string.Empty // pass-through for future providers
            };
        }
    }
}
