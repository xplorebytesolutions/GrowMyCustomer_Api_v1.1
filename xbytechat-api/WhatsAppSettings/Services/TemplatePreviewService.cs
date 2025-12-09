using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat_api.WhatsAppSettings.Services
{
    public sealed class TemplatePreviewService : ITemplatePreviewService
    {
        private readonly IWhatsAppTemplateFetcherService _fetcher;
        private readonly ILogger<TemplatePreviewService> _log;

        public TemplatePreviewService(IWhatsAppTemplateFetcherService fetcher, ILogger<TemplatePreviewService> log)
        {
            _fetcher = fetcher;
            _log = log;
        }

        public async Task<TemplatePreviewResponseDto> PreviewAsync(Guid businessId, TemplatePreviewRequestDto request)
        {
            var resp = new TemplatePreviewResponseDto
            {
                TemplateName = request.TemplateName,
                Language = request.Language
            };

            // 1) Fetch meta (from our catalog)
            var meta = await _fetcher.GetTemplateMetaAsync(
                businessId,
                request.TemplateName,
                language: request.Language,
                provider: request.Provider
            );

            if (meta == null)
            {
                resp.FoundTemplate = false;
                resp.Errors.Add("Template not found for this business/provider/language.");
                return resp;
            }

            resp.FoundTemplate = true;
            resp.Language = meta.Language;
            resp.HasHeaderMedia = meta.HasHeaderMedia;
            resp.HeaderType = meta.HeaderType ?? "";

            // 2) Placeholder validation
            var required = Math.Max(0, meta.BodyPlaceholders?.Count ?? 0);
            var provided = request.TemplateParameters?.Count ?? 0;

            resp.RequiredPlaceholderCount = required;
            resp.ProvidedPlaceholderCount = provided;

            if (provided < required)
            {
                for (int i = provided + 1; i <= required; i++) resp.MissingPlaceholderIndices.Add(i);
                resp.Errors.Add($"Missing {required - provided} body parameter(s).");
            }
            else if (provided > required)
            {
                resp.Warnings.Add($"Ignored {provided - required} extra body parameter(s).");
            }

            // 3) Build provider-like components preview
            var comps = new List<object>();

            // Header (image) preview only when template supports it and caller provided a URL
            if (meta.HasHeaderMedia && !string.IsNullOrWhiteSpace(request.HeaderImageUrl))
            {
                comps.Add(new
                {
                    type = "header",
                    parameters = new object[]
                    {
                        new { type = "image", image = new { link = request.HeaderImageUrl } }
                    }
                });
            }

            // Body parameters: trim/pad to 'required'
            if (required > 0)
            {
                var src = (request.TemplateParameters ?? new List<string>()).Select(s => s ?? string.Empty).ToList();
                if (src.Count > required) src = src.Take(required).ToList();
                while (src.Count < required) src.Add(string.Empty);

                var bodyParams = src.Select(p => (object)new { type = "text", text = p }).ToArray();
                comps.Add(new { type = "body", parameters = bodyParams });
            }

            // 4) Buttons validation (only dynamic URL buttons require parameters in payload)
            // Template order is authoritative. We'll check at most 3.
            var inputByPos = (request.Buttons ?? new List<PreviewButtonInputDto>())
                             .Where(b => b.Position >= 1 && b.Position <= 3)
                             .ToDictionary(b => b.Position, b => b);

            var templateButtons = (meta.Buttons ?? new List<TemplateButtonMeta>())
                                  .OrderBy(b => b.Order)
                                  .Take(3)
                                  .ToList();

            for (int i = 0; i < templateButtons.Count; i++)
            {
                var tb = templateButtons[i];
                var subType = (tb.Type ?? tb.Text ?? "").ToUpperInvariant(); // tb.Type from Step 2.1 mapper; sub-type is treated as URL family
                var paramPattern = tb.Value ?? ""; // came from ButtonsJson ParameterValue (may contain "{{1}}")
                var isUrlFamily = (tb.Type ?? "").Equals("URL", StringComparison.OrdinalIgnoreCase);
                var isDynamic = isUrlFamily && paramPattern.Contains("{{");

                if (!isUrlFamily || !isDynamic)
                {
                    // For static buttons (or non-URL), we preview a button component without parameters
                    comps.Add(new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["sub_type"] = "url",
                        ["index"] = i.ToString()
                    });
                    continue;
                }

                // Dynamic URL → expect input at this position
                if (!inputByPos.TryGetValue(i + 1, out var userBtn) || string.IsNullOrWhiteSpace(userBtn.Value))
                {
                    resp.Errors.Add($"Dynamic URL required for button position {i + 1} but no value was provided.");
                    continue;
                }

                var value = userBtn.Value.Trim();

                // Accept absolute http/https and tel/wa deep links in preview
                var ok = LooksValidDestination(value);
                if (!ok)
                {
                    resp.Errors.Add($"Button {i + 1} destination must be absolute http/https or tel/wa link.");
                    continue;
                }

                // NOTE: In live send we tokenise tracked URLs. For preview we show the **value** directly.
                var parameters = new[] { new Dictionary<string, object> { ["type"] = "text", ["text"] = value } };

                comps.Add(new Dictionary<string, object>
                {
                    ["type"] = "button",
                    ["sub_type"] = "url",
                    ["index"] = i.ToString(),
                    ["parameters"] = parameters
                });
            }

            resp.ProviderComponentsPreview = comps;
            return resp;
        }

        private static bool LooksValidDestination(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim();
            if (s.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("wa:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase)) return true;

            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
                return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            return false;
        }
    }
}
