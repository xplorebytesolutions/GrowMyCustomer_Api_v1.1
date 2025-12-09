using System.Text.RegularExpressions;

namespace xbytechat.api.Features.TemplateModule.Validation;

public static class PlaceholderHelper
{
    private static readonly Regex PlaceholderRx = new(@"\{\{(\d+)\}\}", RegexOptions.Compiled);

    public static List<int> ExtractSlots(string? text)
    {
        var list = new List<int>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (Match m in PlaceholderRx.Matches(text))
        {
            if (int.TryParse(m.Groups[1].Value, out var n)) list.Add(n);
        }
        return list;
    }

    public static (bool ok, string? error) EnsureContinuousFrom1(IReadOnlyCollection<int> slots)
    {
        if (slots.Count == 0) return (true, null);
        var max = slots.Max();
        var set = slots.ToHashSet();
        for (int i = 1; i <= max; i++)
        {
            if (!set.Contains(i)) return (false, $"Missing placeholder {{${i}}} (placeholders must be continuous from {{1}}..{{{max}}}).".Replace("${i}", i.ToString()));
        }
        return (true, null);
    }
}
