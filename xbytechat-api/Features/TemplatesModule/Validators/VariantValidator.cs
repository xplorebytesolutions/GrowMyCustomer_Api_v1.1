using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Validation;
using xbytechat.api.Features.TemplatesModule.Language;

namespace xbytechat.api.Features.TemplateModule.Validators;

public static class VariantValidator
{
    public sealed class Result
    {
        public bool Ok => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public void Add(string msg) { if (!string.IsNullOrWhiteSpace(msg)) Errors.Add(msg); }
    }

    public static Result Validate(TemplateDraftVariantUpsertDto dto)
    {
        var r = new Result();

        // language
        if (!SupportedLanguages.IsSupported(dto.Language))
            r.Add($"Unsupported language: {dto.Language}");

        // body
        if (string.IsNullOrWhiteSpace(dto.BodyText))
            r.Add("BodyText is required.");
        else if (dto.BodyText.Length > TemplateRules.MaxBodyLength)
            r.Add($"BodyText too long (>{TemplateRules.MaxBodyLength}).");

        // placeholders
        var slots = PlaceholderHelper.ExtractSlots(dto.BodyText);
        var cont = PlaceholderHelper.EnsureContinuousFrom1(slots);
        if (!cont.ok) r.Add(cont.error!);

        // header
        if (!TemplateRules.AllowedHeaderTypes.Contains(dto.HeaderType))
            r.Add($"HeaderType must be one of: {string.Join(", ", TemplateRules.AllowedHeaderTypes)}");

        if (dto.HeaderType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(dto.HeaderText))
                r.Add("HeaderText required for TEXT header.");
            else if (dto.HeaderText.Length > TemplateRules.MaxHeaderText)
                r.Add($"HeaderText too long (>{TemplateRules.MaxHeaderText}).");
        }
        else if (!dto.HeaderType.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            // media header types
            if (string.IsNullOrWhiteSpace(dto.HeaderMediaLocalUrl))
                r.Add($"HeaderMediaLocalUrl required for media header type {dto.HeaderType}.");
        }

        // footer
        if (!string.IsNullOrWhiteSpace(dto.FooterText) && dto.FooterText.Length > TemplateRules.MaxFooterText)
            r.Add($"FooterText too long (>{TemplateRules.MaxFooterText}).");

        // buttons
        if (dto.Buttons is not null)
        {
            if (dto.Buttons.Count > TemplateRules.MaxButtons)
                r.Add($"At most {TemplateRules.MaxButtons} buttons are allowed.");

            int phoneCtas = 0;
            var quickReplyTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in dto.Buttons)
            {
                if (!TemplateRules.AllowedButtonTypes.Contains(b.Type))
                {
                    r.Add($"Button type '{b.Type}' is invalid.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(b.Text))
                {
                    r.Add("Button text is required.");
                }
                else if (b.Type.Equals("QUICK_REPLY", StringComparison.OrdinalIgnoreCase))
                {
                    if (b.Text.Length > TemplateRules.MaxQuickReplyText)
                        r.Add($"Quick Reply text too long (>{TemplateRules.MaxQuickReplyText}).");
                    if (!quickReplyTexts.Add(b.Text))
                        r.Add("Duplicate Quick Reply button text not allowed.");
                }

                if (b.Type.Equals("URL", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TemplateRules.IsValidUrl(b.Url))
                        r.Add("URL button must have a valid http(s) Url.");
                }

                if (b.Type.Equals("PHONE", StringComparison.OrdinalIgnoreCase))
                {
                    phoneCtas++;
                    if (!TemplateRules.IsValidPhone(b.Phone))
                        r.Add("PHONE button must have a valid phone number.");
                }
            }

            if (phoneCtas > 1) r.Add("Only one PHONE CTA button is allowed.");
        }

        // examples for placeholders
        if (slots.Count > 0)
        {
            var need = slots.Max();
            var missing = new List<string>();
            for (int i = 1; i <= need; i++)
            {
                if (dto.Examples is null || !dto.Examples.ContainsKey(i.ToString()) || string.IsNullOrWhiteSpace(dto.Examples[i.ToString()]))
                    missing.Add($"{{{{{i}}}}}");
            }
            if (missing.Count > 0)
                r.Add($"Examples required for placeholders: {string.Join(", ", missing)}");
        }

        return r;
    }
}
