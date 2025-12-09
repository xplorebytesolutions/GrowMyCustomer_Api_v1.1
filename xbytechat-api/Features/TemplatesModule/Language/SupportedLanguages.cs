namespace xbytechat.api.Features.TemplatesModule.Language;


public static class SupportedLanguages
{
    // Seed a small set; we’ll extend when needed.
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "en_US", // English (US)
        "hi_IN", // Hindi (India)
        // add more later: "en_GB", "mr_IN", "bn_IN", ...
    };

    public static bool IsSupported(string? code)
        => !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
}

