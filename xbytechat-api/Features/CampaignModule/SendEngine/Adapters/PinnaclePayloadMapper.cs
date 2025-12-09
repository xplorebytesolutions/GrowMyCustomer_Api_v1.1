using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Pinnacle;

namespace xbytechat.api.Features.CampaignModule.SendEngine
{
    public sealed class PinnaclePayloadMapper : IProviderPayloadMapper
    {
        public object BuildPayload(SendPlan plan, RecipientPlan recipient, TemplateEnvelope env)
        {
            var components = BuildComponents(env);

            var msg = new PinnacleTemplateMessage(
                MessagingProduct: "whatsapp",
                To: recipient.ToPhoneE164,
                Type: "template",
                Template: new PinnacleTemplate(
                    Name: plan.TemplateName,
                    Language: new PinnacleLanguage(plan.LanguageCode),
                    Components: components
                )
            );

            return msg; // typed record; engine serializes via JsonCtx
        }

        // Build fully-typed components for Pinnacle (camelCase subType, numeric index)
        private static IReadOnlyList<PinnacleComponentBase> BuildComponents(TemplateEnvelope env)
        {
            var list = new List<PinnacleComponentBase>(capacity: 4);

            // ===== HEADER =====
            if (env.HeaderKind != HeaderKind.None)
            {
                switch (env.HeaderKind)
                {
                    // TEXT header uses HeaderParams (0..N)
                    case HeaderKind.Text when env.HeaderParams is { Count: > 0 }:
                        {
                            var pars = new PinnTextParam[env.HeaderParams.Count];
                            for (int i = 0; i < env.HeaderParams.Count; i++)
                                pars[i] = new PinnTextParam("text", env.HeaderParams[i] ?? string.Empty);

                            list.Add(new PinnHeaderTextComponent(
                                Type: "header",
                                Parameters: pars
                            ));
                            break;
                        }

                    case HeaderKind.Image when !string.IsNullOrWhiteSpace(env.HeaderUrl):
                        list.Add(new PinnHeaderImageComponent(
                            Type: "header",
                            Parameters: new[] { new PinnMediaParam("image", Image: new PinnMediaLink(env.HeaderUrl!)) }
                        ));
                        break;

                    case HeaderKind.Video when !string.IsNullOrWhiteSpace(env.HeaderUrl):
                        list.Add(new PinnHeaderVideoComponent(
                            Type: "header",
                            Parameters: new[] { new PinnMediaParam("video", Video: new PinnMediaLink(env.HeaderUrl!)) }
                        ));
                        break;

                    case HeaderKind.Document when !string.IsNullOrWhiteSpace(env.HeaderUrl):
                        list.Add(new PinnHeaderDocumentComponent(
                            Type: "header",
                            Parameters: new[] { new PinnMediaParam("document", Document: new PinnMediaLink(env.HeaderUrl!)) }
                        ));
                        break;
                }
            }

            // ===== BODY =====
            if (env.BodyParams is { Count: > 0 })
            {
                var pars = new PinnTextParam[env.BodyParams.Count];
                for (int i = 0; i < env.BodyParams.Count; i++)
                    pars[i] = new PinnTextParam("text", env.BodyParams[i] ?? string.Empty);

                list.Add(new PinnBodyComponent(
                    Type: "body",
                    Parameters: pars
                ));
            }

            // ===== BUTTONS — URL only =====
            var dyn = ReadPerRecipientButtonParams(env); // prefer dict, fallback to list

            var buttonsCount = env.Buttons?.Count ?? 0;
            if (buttonsCount > 0)
            {
                var max = Math.Min(3, buttonsCount);
                for (int i = 0; i < max; i++)
                {
                    var meta = env.Buttons![i];
                    if (!string.Equals(meta.Type, "url", StringComparison.OrdinalIgnoreCase))
                        continue; // only URL buttons consume a dynamic "param"

                    PinnTextParam[]? parameters = null;
                    var isDynamic = !string.IsNullOrWhiteSpace(meta.TargetUrl) && meta.TargetUrl.IndexOf("{{", StringComparison.Ordinal) >= 0;
                    if (isDynamic)
                    {
                        // index i => position (i+1) in canonical dict; ReadPerRecipientButtonParams already ordered by pos
                        var pv = i < dyn.Count ? dyn[i] ?? string.Empty : string.Empty;
                        parameters = new[] { new PinnTextParam("text", pv) };
                    }

                    list.Add(new PinnButtonUrlComponent(
                        Type: "button",
                        SubType: "url",
                        Index: i,                 // numeric index for Pinnacle (0,1,2)
                        Parameters: parameters    // null for static, one text param for dynamic
                    ));
                }
            }

            return list;
        }

        // Prefer canonical dict: { "button1.url_param": "X", "button2.url_param": "Y", ... }
        // Fallback to legacy array: ["X","Y","Z"]
        private static IReadOnlyList<string> ReadPerRecipientButtonParams(TemplateEnvelope env)
        {
            var json = env.PerRecipientButtonParamsJson;
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            // 1) Try dict first (canonical)
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json!);
                if (dict is { Count: > 0 })
                {
                    var list = new List<string>(capacity: 3);
                    for (int pos = 1; pos <= 3; pos++)
                    {
                        if (dict.TryGetValue($"button{pos}.url_param", out var v) && !string.IsNullOrWhiteSpace(v))
                            list.Add(v);
                    }
                    return list;
                }
            }
            catch
            {
                // fall through to array
            }

            // 2) Legacy array fallback
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(json!);
                if (arr is null || arr.Count == 0) return Array.Empty<string>();
                // Trim empties, preserve order (index => button index)
                var cleaned = new List<string>(capacity: Math.Min(3, arr.Count));
                for (int i = 0; i < arr.Count && i < 3; i++)
                {
                    var s = arr[i];
                    if (!string.IsNullOrWhiteSpace(s)) cleaned.Add(s);
                }
                return cleaned;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
