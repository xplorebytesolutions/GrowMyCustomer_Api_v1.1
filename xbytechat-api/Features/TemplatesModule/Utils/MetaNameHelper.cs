using System.Text;
using System.Text.RegularExpressions;

namespace xbytechat.api.Features.TemplateModule.Utils;

public static class MetaNameHelper
{
    // WhatsApp template name rules: lowercase, underscores, alnum and _ only, <= 25 chars.
    private static readonly Regex Allowed = new(@"[^a-z0-9_]+", RegexOptions.Compiled);

    public static string FromKey(string key, Guid businessId, string? suffix = null)
    {
        // 1) lower + underscores
        var s = key.Trim().ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');

        // 2) strip invalids
        s = Allowed.Replace(s, "");

        // 3) ensure starts with a letter
        if (s.Length == 0 || !char.IsLetter(s[0]))
            s = "t_" + s;

        // 4) add optional suffix for uniqueness (e.g., short biz hash)
        if (!string.IsNullOrWhiteSpace(suffix))
            s = $"{s}_{suffix}".TrimEnd('_');

        // 5) cap length 25 (WA limit)
        if (s.Length > 25) s = s[..25];

        // fallback safety
        if (string.IsNullOrWhiteSpace(s)) s = "t_" + businessId.ToString("N")[..6];
        return s;
    }

    public static string ShortBizSuffix(Guid businessId)
        => businessId.ToString("N")[..6];
}
