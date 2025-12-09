using System.Text.Json;
using xbytechat.api.Features.TemplateModule.DTOs;

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
                    text = headerText ?? string.Empty
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

        // Body
        components.Add(new
        {
            type = "BODY",
            text = bodyText
        });

        // Footer
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            components.Add(new
            {
                type = "FOOTER",
                text = footerText
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
                    btns.Add(new { type = "QUICK_REPLY", text = b.Text });
                }
                else if (b.Type.Equals("URL", StringComparison.OrdinalIgnoreCase))
                {
                    btns.Add(new { type = "URL", text = b.Text, url = b.Url });
                }
                else if (b.Type.Equals("PHONE", StringComparison.OrdinalIgnoreCase))
                {
                    btns.Add(new { type = "PHONE_NUMBER", text = b.Text, phone_number = b.Phone });
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

        // Examples: WA expects arrays per component; for body variables, supply example text array
        // We map {"1":"John","2":"12345"} -> [["John","12345"]]
        var maxIndex = 0;
        foreach (var k in examplesMap.Keys)
        {
            if (int.TryParse(k, out var n) && n > maxIndex) maxIndex = n;
        }
        var bodyExampleRow = new List<string>();
        for (int i = 1; i <= maxIndex; i++)
        {
            examplesMap.TryGetValue(i.ToString(), out var val);
            bodyExampleRow.Add(val ?? "");
        }

        var examples = new
        {
            // aligns with newer Graph requirements (body_text examples)
            // bodyExampleRow: List<string>  -> convert to string[] so both branches are string[][]
            body_text = bodyExampleRow.Count > 0
         ? new[] { bodyExampleRow.ToArray() }
         : Array.Empty<string[]>()
        };


        return (components, examples);
    }
}
