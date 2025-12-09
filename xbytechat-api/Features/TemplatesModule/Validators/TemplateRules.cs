namespace xbytechat.api.Features.TemplateModule.Validation;

public static class TemplateRules
{
    public const int MaxTemplateName = 25;              // applies when we later derive Meta name from Key
    public const int MaxButtons = 3;
    public const int MaxQuickReplyText = 25;            // conservative; WA may allow ~20-25
    public const int MaxHeaderText = 60;                // conservative header text length
    public const int MaxFooterText = 60;                // conservative footer text length
    public const int MaxBodyLength = 1024;              // safe upper bound for WA template body

    public static readonly HashSet<string> AllowedHeaderTypes = new(StringComparer.OrdinalIgnoreCase)
    { "NONE", "TEXT", "IMAGE", "VIDEO", "DOCUMENT" };

    public static readonly HashSet<string> AllowedButtonTypes = new(StringComparer.OrdinalIgnoreCase)
    { "QUICK_REPLY", "URL", "PHONE" };

    public static bool IsValidPhone(string? phone)
        => !string.IsNullOrWhiteSpace(phone) && phone!.Trim().Length >= 7 && phone.Trim().Length <= 20;

    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var u)
               && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
    }
}
