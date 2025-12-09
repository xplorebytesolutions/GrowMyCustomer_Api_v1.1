using System.Text.Json;
using xbytechat.api.Features.TemplateModule.Validation;
using xbytechat.api.Features.TemplateModule.DTOs;

namespace xbytechat.api.Features.TemplateModule.Validators;

public static class MultiLanguageValidator
{
    public sealed class VariantView
    {
        public required string Language { get; init; }
        public required string BodyText { get; init; }
        public required string HeaderType { get; init; }
        public string? HeaderText { get; init; }
        public string? HeaderMediaLocalUrl { get; init; }
        public string? FooterText { get; init; }
        public required List<ButtonDto> Buttons { get; init; }
        public required Dictionary<string, string> Examples { get; init; }
    }

    public static (bool ok, Dictionary<string, List<string>> errors) Validate(IReadOnlyList<VariantView> variants)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        bool OkLang(string lang) => !errors.ContainsKey(lang) || errors[lang].Count == 0;
        void Add(string lang, string msg)
        {
            if (!errors.TryGetValue(lang, out var list)) { list = new(); errors[lang] = list; }
            list.Add(msg);
        }

        // 1) Per-variant rules (reuse VariantValidator)
        foreach (var v in variants)
        {
            var dto = new TemplateDraftVariantUpsertDto
            {
                Language = v.Language,
                BodyText = v.BodyText,
                HeaderType = v.HeaderType,
                HeaderText = v.HeaderText,
                HeaderMediaLocalUrl = v.HeaderMediaLocalUrl,
                FooterText = v.FooterText,
                Buttons = v.Buttons,
                Examples = v.Examples
            };
            var r = VariantValidator.Validate(dto);
            foreach (var e in r.Errors) Add(v.Language, e);
        }

        // 2) Cross-language placeholder arity/order parity
        if (variants.Count > 1)
        {
            var reference = variants[0];
            var refSlots = PlaceholderHelper.ExtractSlots(reference.BodyText);
            var refSet = refSlots.ToHashSet();

            foreach (var v in variants.Skip(1))
            {
                var slots = PlaceholderHelper.ExtractSlots(v.BodyText);
                var set = slots.ToHashSet();

                // arity parity
                if (set.Count != refSet.Count)
                    Add(v.Language, $"Placeholder count mismatch vs {reference.Language}: expected {refSet.Count}, got {set.Count}.");

                // exact set parity (e.g., {1,2,3})
                if (!set.SetEquals(refSet))
                    Add(v.Language, $"Placeholder indices mismatch vs {reference.Language}: expected {{{string.Join(",", refSet.OrderBy(x => x))}}}.");

                // continuity check already handled per-variant; this ensures cross-lang equality.
            }
        }

        var ok = errors.Values.All(l => l.Count == 0);
        return (ok, errors);
    }
}
