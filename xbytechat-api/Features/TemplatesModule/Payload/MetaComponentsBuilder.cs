using System.Text.Json;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Validation;

namespace xbytechat.api.Features.TemplateModule.Payload;

public static class MetaComponentsBuilder
{
    // Builds a minimal Graph payload compatible structure.
    // We return "object" (anonymous shape) so the Meta client can serialize directly.
    public static (object components, object examples) Build(
        string headerType,
        string? headerText,
        string? headerMetaMediaId,
        string bodyText,
        string? footerText,
        List<ButtonDto> buttons,
        Dictionary<string, string> examplesMap)
    {
        // Header
        var components = new List<object>();

        if (!headerType.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            if (headerType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
            {
                components.Add(new
                {
                    type = "HEADER",
                    format = "TEXT",
                    text = (headerText ?? string.Empty).Trim()
                });
            }
            else
            {
                // IMAGE|VIDEO|DOCUMENT
                components.Add(new
                {
                    type = "HEADER",
                    format = headerType.ToUpperInvariant(),
                    example = new
                    {
                        header_handle = new[] { headerMetaMediaId ?? string.Empty }
                    }
                });
            }
        }

        // Body (include examples for placeholders inside BODY.example as required by Meta)
        var bodySlots = PlaceholderHelper.ExtractSlots(bodyText);
        var maxIndex = bodySlots.Count > 0 ? bodySlots.Max() : 0;
        var bodyExampleRow = new List<string>();
        for (int i = 1; i <= maxIndex; i++)
        {
            examplesMap.TryGetValue(i.ToString(), out var val);
            bodyExampleRow.Add(val ?? "");
        }

        components.Add(bodyExampleRow.Count > 0
            ? new
            {
                type = "BODY",
                text = bodyText.Trim(),
                example = new
                {
                    body_text = new[] { bodyExampleRow.ToArray() }
                }
            }
            : new
            {
                type = "BODY",
                text = bodyText.Trim()
            });

        // Footer
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            components.Add(new
            {
                type = "FOOTER",
                text = footerText.Trim()
            });
        }

        // Buttons
        if (buttons is { Count: > 0 })
        {
            var btns = new List<object>();
            foreach (var b in buttons)
            {
                if (b.Type.Equals("QUICK_REPLY", StringComparison.OrdinalIgnoreCase))
                {
                    btns.Add(new { type = "QUICK_REPLY", text = b.Text.Trim() });
                }
                else if (b.Type.Equals("URL", StringComparison.OrdinalIgnoreCase))
                {
                    btns.Add(new { type = "URL", text = b.Text.Trim(), url = (b.Url ?? "").Trim() });
                }
                else if (b.Type.Equals("PHONE", StringComparison.OrdinalIgnoreCase))
                {
                    btns.Add(new { type = "PHONE_NUMBER", text = b.Text.Trim(), phone_number = (b.Phone ?? "").Trim() });
                }
            }

            if (btns.Count > 0)
            {
                components.Add(new
                {
                    type = "BUTTONS",
                    buttons = btns
                });
            }
        }

        // Keep a top-level examples projection for UI/debug (Meta requires it inside BODY.example).
        var examples = new
        {
            body_text = bodyExampleRow.Count > 0
                ? new[] { bodyExampleRow.ToArray() }
                : Array.Empty<string[]>()
        };


        return (components, examples);
    }
}
