using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace xbytechat.api.WhatsAppSettings.Helpers
{
    public static class TemplateJsonHelper
    {
        // Positional like {{1}}
        private static readonly Regex PositionalRx = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);
        // Named like {{first_name}}  (letters, digits, _, -, starting with letter/_)
        private static readonly Regex NamedRx = new(@"\{\{\s*([A-Za-z_][A-Za-z0-9_\-]*)\s*\}\}", RegexOptions.Compiled);
        // Any like {{...}} — used for total counting / empty braces
        private static readonly Regex AnyRx = new(@"\{\{[^}]*\}\}", RegexOptions.Compiled);

        public static (string HeaderKind, string CombinedBody, int PlaceholderCount)
            Summarize(string rawJson, string? bodyFallback = null)
        {
            var d = SummarizeDetailed(rawJson, bodyFallback);
            return (d.HeaderKind ?? "none", d.CombinedPreviewBody ?? string.Empty, d.PlaceholderCount);
        }

        public static TemplateSummaryDto SummarizeDetailed(string rawJson, string? bodyFallback = null)
        {
            var doc = JToken.Parse(string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
            var components = doc["components"] as JArray ?? new JArray();

            // Provider hint (may be null/empty)
            var parameterFormatHint = doc["parameter_format"]?.ToString()?.ToUpperInvariant(); // "POSITIONAL" | "NAMED" | null

            string headerKind = "none";
            string? headerText = null;
            string? bodyText = null;

            var headerPosIdx = new List<int>();
            var bodyPosIdx = new List<int>();
            var buttons = new List<ButtonParamTemplate>();
            var occurrences = new List<PlaceholderOccurrence>();

            int order = 0;

            foreach (var comp in components)
            {
                var type = comp?["type"]?.ToString()?.ToUpperInvariant();
                if (string.IsNullOrEmpty(type)) continue;

                if (type == "HEADER")
                {
                    var format = comp?["format"]?.ToString()?.ToUpperInvariant();
                    headerKind = format switch
                    {
                        "TEXT" => "text",
                        "IMAGE" => "image",
                        "VIDEO" => "video",
                        "DOCUMENT" => "document",
                        _ => "none"
                    };

                    if (headerKind == "text")
                    {
                        headerText = comp?["text"]?.ToString() ?? "";
                        headerPosIdx = ExtractPositional(headerText);
                        occurrences.AddRange(CollectOccurrences(headerText, PlaceholderLocation.Header));
                    }
                }
                else if (type == "BODY")
                {
                    bodyText = comp?["text"]?.ToString() ?? "";
                    bodyPosIdx = ExtractPositional(bodyText);
                    occurrences.AddRange(CollectOccurrences(bodyText, PlaceholderLocation.Body));
                }
                else if (type == "BUTTONS")
                {
                    var btns = comp?["buttons"] as JArray ?? new JArray();
                    foreach (var b in btns)
                    {
                        order++;
                        var btnType = b?["type"]?.ToString()?.ToUpperInvariant() ?? "";
                        var text = b?["text"]?.ToString() ?? "";

                        string? urlLike =
                            b?["url"]?.ToString()
                            ?? b?["phone_number"]?.ToString()
                            ?? b?["coupon_code"]?.ToString()
                            ?? b?["flow_id"]?.ToString();

                        // “example” can be string or array
                        if (string.IsNullOrWhiteSpace(urlLike))
                        {
                            var ex = b?["example"];
                            if (ex is JArray arr) urlLike = arr.FirstOrDefault()?.ToString();
                            else if (ex is JValue val) urlLike = val.ToString();
                        }

                        var tmpl = urlLike;
                        var posIdx = string.IsNullOrEmpty(tmpl) ? new List<int>() : ExtractPositional(tmpl);

                        buttons.Add(new ButtonParamTemplate
                        {
                            Order = order,
                            Type = btnType,
                            Text = text,
                            ParamTemplate = tmpl,
                            ParamIndices = posIdx
                        });

                        // collect occurrences for button text & param template separately
                        if (!string.IsNullOrEmpty(text))
                            occurrences.AddRange(CollectOccurrences(text, PlaceholderLocation.Button, order, "text"));

                        if (!string.IsNullOrEmpty(tmpl))
                            occurrences.AddRange(CollectOccurrences(tmpl!, PlaceholderLocation.Button, order, "param"));
                    }
                }
            }

            // fallback if no BODY block present in JSON
            bodyText ??= bodyFallback ?? "";

            var combined = CombinePreview(headerText, bodyText);

            // Positional counting → max index across header/body/buttons
            var maxHeader = headerPosIdx.DefaultIfEmpty(0).Max();
            var maxBody = bodyPosIdx.DefaultIfEmpty(0).Max();
            var maxBtns = buttons.SelectMany(b => b.ParamIndices).DefaultIfEmpty(0).Max();
            var maxPositional = Math.Max(maxHeader, Math.Max(maxBody, maxBtns));

            int placeholderCount;
            if (maxPositional > 0)
            {
                placeholderCount = maxPositional;
            }
            else
            {
                // Named/empty mode → total {{...}} across header/body/buttons
                placeholderCount =
                    CountAny(headerText) +
                    CountAny(bodyText) +
                    buttons.Sum(b => CountAny(b.ParamTemplate)) +
                    // button text may also contain placeholders
                    buttons.Sum(b => CountAny(b.Text));
            }

            // ----- ParameterFormat resolution (hint → inference) -----
            string? parameterFormat = null;
            if (parameterFormatHint == "POSITIONAL" || parameterFormatHint == "NAMED")
            {
                parameterFormat = parameterFormatHint;
            }
            else
            {
                // Infer from content
                var hasPositional = maxPositional > 0;
                var hasNamed = occurrences.Any(o => o.Type == PlaceholderType.Named);

                if (hasPositional) parameterFormat = "POSITIONAL";
                else if (hasNamed) parameterFormat = "NAMED";
                else parameterFormat = null; // unknown / no placeholders
            }

            return new TemplateSummaryDto
            {
                HeaderKind = headerKind,
                HeaderText = headerText,
                BodyText = bodyText,
                CombinedPreviewBody = combined,
                HeaderParamIndices = headerPosIdx,
                BodyParamIndices = bodyPosIdx,
                Buttons = buttons,
                PlaceholderCount = placeholderCount,
                Placeholders = occurrences,

                // NEW: summary flag so services don't have to re-derive this
                ParameterFormat = parameterFormat
            };
        }


        /// <summary>Render a text with positional placeholders ({{1}}, {{2}}, …).</summary>
        public static string RenderTextPositional(string? template, IReadOnlyDictionary<int, string> args)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            // Replace highest-first to avoid {{1}} touching characters that form {{10}}
            // Using Regex evaluator is safer:
            return PositionalRx.Replace(template, m =>
            {
                var idx = int.Parse(m.Groups[1].Value);
                return args.TryGetValue(idx, out var val) ? val ?? string.Empty : string.Empty;
            });
        }

        /// <summary>Render a text with named placeholders ({{name}}, {{email}}). Empty {{}} treated as next positional index (1-based).</summary>
        public static string RenderTextNamed(string? template, IReadOnlyDictionary<string, string> namedArgs, IReadOnlyDictionary<int, string>? positionalArgs = null)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            // First replace named
            var result = NamedRx.Replace(template, m =>
            {
                var key = m.Groups[1].Value;
                return namedArgs.TryGetValue(key, out var val) ? val ?? string.Empty : string.Empty;
            });

            // Then positional if provided
            if (positionalArgs != null && positionalArgs.Count > 0)
            {
                result = RenderTextPositional(result, positionalArgs);
            }

            // Finally convert empty {{}} → empty string (or you could plug a policy here)
            result = Regex.Replace(result, @"\{\{\s*\}\}", string.Empty);

            return result;
        }

        // ---------- internal helpers ----------

        private static List<int> ExtractPositional(string text)
            => PositionalRx.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();

        private static int CountAny(string? text)
            => string.IsNullOrEmpty(text) ? 0 : AnyRx.Matches(text).Count;

        private static string CombinePreview(string? header, string? body)
        {
            var h = string.IsNullOrWhiteSpace(header) ? null : header!.Trim();
            var b = string.IsNullOrWhiteSpace(body) ? null : body!.Trim();
            if (h is null && b is null) return "";
            if (h is null) return b!;
            if (b is null) return h!;
            return $"{h}\n\n{b}";
        }

        private static IEnumerable<PlaceholderOccurrence> CollectOccurrences(
            string text,
            PlaceholderLocation location,
            int? buttonOrder = null,
            string? buttonField = null)
        {
            // Positional
            foreach (Match m in PositionalRx.Matches(text))
            {
                yield return new PlaceholderOccurrence
                {
                    Type = PlaceholderType.Positional,
                    Index = int.Parse(m.Groups[1].Value),
                    Raw = m.Value,
                    Location = location,
                    ButtonOrder = buttonOrder,
                    ButtonField = buttonField
                };
            }
            // Named
            foreach (Match m in NamedRx.Matches(text))
            {
                yield return new PlaceholderOccurrence
                {
                    Type = PlaceholderType.Named,
                    Name = m.Groups[1].Value,
                    Raw = m.Value,
                    Location = location,
                    ButtonOrder = buttonOrder,
                    ButtonField = buttonField
                };
            }
            // Empty braces (that weren’t positional or named)
            // We count them by subtracting positional+named from all-any.
            var any = AnyRx.Matches(text).Cast<Match>().Select(mm => mm.Value).ToList();
            var consumed = new HashSet<string>(PositionalRx.Matches(text).Cast<Match>().Select(mm => mm.Value)
                .Concat(NamedRx.Matches(text).Cast<Match>().Select(mm => mm.Value)));
            foreach (var token in any.Where(t => !consumed.Contains(t)))
            {
                yield return new PlaceholderOccurrence
                {
                    Type = PlaceholderType.Empty,
                    Raw = token,
                    Location = location,
                    ButtonOrder = buttonOrder,
                    ButtonField = buttonField
                };
            }
        }
    }

    // ---------- DTOs ----------

    public sealed class TemplateSummaryDto
    {
        public string HeaderKind { get; set; } = "none";
        public string? HeaderText { get; set; }
        public string? BodyText { get; set; }

        public List<int> HeaderParamIndices { get; set; } = new();
        public List<int> BodyParamIndices { get; set; } = new();

        public List<ButtonParamTemplate> Buttons { get; set; } = new();

        public string? CombinedPreviewBody { get; set; }

        public int PlaceholderCount { get; set; }

        public List<PlaceholderOccurrence> Placeholders { get; set; } = new();
        public string ParameterFormat { get; set; } = "UNKNOWN"; // POSITIONAL | NAMED | MIXED | UNKNOWN

    }

    public sealed class ButtonParamTemplate
    {
        public int Order { get; set; }
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
        public string? ParamTemplate { get; set; }
        public List<int> ParamIndices { get; set; } = new();
    }

    public enum PlaceholderType { Positional, Named, Empty }

    public enum PlaceholderLocation {

        Header = 0, Body = 1, Button = 2
    }

    public sealed class PlaceholderOccurrence
    {
        public PlaceholderType Type { get; set; }
        public int? Index { get; set; }     // for Positional
        public string? Name { get; set; }   // for Named
        public string Raw { get; set; } = "{{}}";
        public PlaceholderLocation Location { get; set; }
        public int? ButtonOrder { get; set; }     // when Location == Button
        public string? ButtonField { get; set; }  // "text" or "param"
    }
}
