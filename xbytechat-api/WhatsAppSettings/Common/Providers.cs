namespace xbytechat.api.WhatsAppSettings.Common
{
    public static class Providers
    {
        public const string PINNACLE = "PINNACLE";
        public const string META_CLOUD = "META_CLOUD";

        public static string NormalizeToUpper(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return PINNACLE;
            var s = raw.Trim().ToUpperInvariant();
            return s switch
            {
                "META" => META_CLOUD,
                "META_CLOUD" => META_CLOUD,
                "PINNACLE" => PINNACLE,
                _ => s // future providers just uppercase
            };
        }
        public static bool IsValid(string? raw)
        {
            var s = NormalizeToUpper(raw);
            return s == PINNACLE || s == META_CLOUD;
        }
    }
}
