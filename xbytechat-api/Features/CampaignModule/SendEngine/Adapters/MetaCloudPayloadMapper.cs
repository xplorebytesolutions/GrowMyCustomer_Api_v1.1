using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta;

namespace xbytechat.api.Features.CampaignModule.SendEngine
{
    public sealed class MetaCloudPayloadMapper : IProviderPayloadMapper
    {
        private readonly ILogger<MetaCloudPayloadMapper> _log;

        public MetaCloudPayloadMapper(ILogger<MetaCloudPayloadMapper> log)
        {
            _log = log;
        }

        public object BuildPayload(SendPlan plan, RecipientPlan recipient, TemplateEnvelope env)
        {
            var to = recipient?.ToPhoneE164 ?? "";
            _log.LogInformation("MetaMapper: BuildPayload start | template={Template} lang={Lang} to={To}",
                plan?.TemplateName, plan?.LanguageCode, to);

            try
            {
                var components = BuildComponents(env);

                var msg = new MetaTemplateMessage(
                    MessagingProduct: "whatsapp",
                    To: to,
                    Type: "template",
                    Template: new MetaTemplate(
                        Name: plan.TemplateName,
                        // Use exactly what you saved in DB (e.g., "en_US")
                        Language: new MetaLanguage(plan.LanguageCode),
                        Components: components
                    )
                );

                _log.LogDebug("MetaMapper: Components built | headerKind={HeaderKind} bodyParams={BodyCount} buttons={BtnCount}",
                    env.HeaderKind, env.BodyParams?.Count ?? 0, env.Buttons?.Count ?? 0);

                return msg;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "MetaMapper: Failed to build payload | template={Template} lang={Lang} to={To}",
                    plan?.TemplateName, plan?.LanguageCode, to);
                throw;
            }
        }

        // Build typed Meta components (header/body/buttons)
        private IReadOnlyList<MetaComponentBase> BuildComponents(TemplateEnvelope env)
        {
            var list = new List<MetaComponentBase>(capacity: 4);

            // ----- HEADER -----
            if (env.HeaderKind != HeaderKind.None)
            {
                switch (env.HeaderKind)
                {
                    case HeaderKind.Text:
                        if (env.HeaderParams is { Count: > 0 })
                        {
                            _log.LogDebug("MetaMapper: Header=text | params={Count}", env.HeaderParams.Count);
                            list.Add(new MetaHeaderTextComponent(
                                Type: "header",
                                Parameters: BuildTextParams(env.HeaderParams, "header")
                            ));
                        }
                        else
                        {
                            _log.LogDebug("MetaMapper: Header=text but no params present");
                        }
                        break;

                    case HeaderKind.Image:
                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
                        {
                            _log.LogDebug("MetaMapper: Header=image | url set");
                            list.Add(new MetaHeaderImageComponent(
                                Type: "header",
                                Parameters: new[]
                                {
                                    new MetaMediaParam("image", Image: new MetaMediaLink(env.HeaderUrl!))
                                }
                            ));
                        }
                        else
                        {
                            _log.LogWarning("MetaMapper: Header=image but HeaderUrl is missing/empty");
                        }
                        break;

                    case HeaderKind.Video:
                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
                        {
                            _log.LogDebug("MetaMapper: Header=video | url set");
                            list.Add(new MetaHeaderVideoComponent(
                                Type: "header",
                                Parameters: new[]
                                {
                                    new MetaMediaParam("video", Video: new MetaMediaLink(env.HeaderUrl!))
                                }
                            ));
                        }
                        else
                        {
                            _log.LogWarning("MetaMapper: Header=video but HeaderUrl is missing/empty");
                        }
                        break;

                    case HeaderKind.Document:
                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
                        {
                            _log.LogDebug("MetaMapper: Header=document | url set");
                            list.Add(new MetaHeaderDocumentComponent(
                                Type: "header",
                                Parameters: new[]
                                {
                                    new MetaMediaParam("document", Document: new MetaMediaLink(env.HeaderUrl!))
                                }
                            ));
                        }
                        else
                        {
                            _log.LogWarning("MetaMapper: Header=document but HeaderUrl is missing/empty");
                        }
                        break;
                }
            }

            // ----- BODY -----
            if (env.BodyParams is { Count: > 0 })
            {
                _log.LogDebug("MetaMapper: Body params={Count}", env.BodyParams.Count);
                list.Add(new MetaBodyComponent(
                    Type: "body",
                    Parameters: BuildTextParams(env.BodyParams, "body")
                ));
            }
            else
            {
                _log.LogDebug("MetaMapper: No body params present");
            }

            // ----- BUTTONS (URL only) -----
            var perRecipient = ParsePerRecipient(env.PerRecipientButtonParamsJson);

            for (int i = 0; i < (env.Buttons?.Count ?? 0) && i < 3; i++)
            {
                var b = env.Buttons[i];
                if (!string.Equals(b.Type, "url", StringComparison.OrdinalIgnoreCase))
                    continue;

                var idxStr = i.ToString(); // Meta uses "0" | "1" | "2"
                var key = $"button{i + 1}.url_param";
                MetaTextParam[]? parameters = null;

                if (perRecipient.TryGetValue(key, out var dyn))
                {
                    if (string.IsNullOrWhiteSpace(dyn))
                    {
                        _log.LogWarning("MetaMapper: URL button param is empty | index={Index} key={Key}", i, key);
                        // Leave parameters=null; if template actually requires a dynamic value,
                        // Meta will 400. Better to fail earlier in your validation pipeline if possible.
                    }
                    else
                    {
                        parameters = new[] { new MetaTextParam("text", dyn) };
                        _log.LogDebug("MetaMapper: URL button dynamic param set | index={Index} key={Key}", i, key);
                    }
                }
                else
                {
                    _log.LogDebug("MetaMapper: URL button has no dynamic param in payload | index={Index} key={Key}", i, key);
                }

                list.Add(new MetaButtonUrlComponent(
                    Type: "button",
                    SubType: "url",
                    Index: idxStr,
                    Parameters: parameters
                ));
            }

            return list;
        }

        /// <summary>
        /// Build Meta "text" parameters ensuring no empty/whitespace values are sent.
        /// Throws InvalidOperationException with a clear message if any value is missing.
        /// </summary>
        private MetaTextParam[] BuildTextParams(IReadOnlyList<string> values, string componentName)
        {
            if (values is null || values.Count == 0)
                return Array.Empty<MetaTextParam>();

            var result = new List<MetaTextParam>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (string.IsNullOrWhiteSpace(v))
                {
                    _log.LogWarning("MetaMapper: Empty placeholder value detected | component={Component} index={Index}", componentName, i + 1);
                    // Fail early instead of hitting Meta with bad payload -> avoids 400 (#131008)
                    throw new InvalidOperationException(
                        $"Template {componentName} parameter #{i + 1} is empty/whitespace. " +
                        $"Provide a value for all placeholders required by the template.");
                }
                result.Add(new MetaTextParam("text", v));
            }
            return result.ToArray();
        }

        // === Lenient parser: never throws; supports {}, [] forms; returns empty on any issue.
        private Dictionary<string, string> ParsePerRecipient(string? json)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(json))
                return map;

            // Try object form first: { "button1.url_param": "https://...", "code":"ABC" }
            try
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(json!);
                if (obj != null)
                {
                    foreach (var kv in obj)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key))
                            map[kv.Key.Trim()] = kv.Value ?? string.Empty;
                    }
                    return map;
                }
            }
            catch
            {
                // fall through to array form
            }

            // Try array form: [ { "key":"button1.url_param", "value":"https://..." }, ... ]
            try
            {
                var arr = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json!);
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        if (!item.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
                            continue;
                        item.TryGetValue("value", out var value);
                        map[key.Trim()] = value ?? string.Empty;
                    }
                }
            }
            catch
            {
                // swallow – return empty for malformed payload
            }

            return map;
        }
    }
}



//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.Json;
//using Microsoft.Extensions.Logging;
//using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta;

//namespace xbytechat.api.Features.CampaignModule.SendEngine
//{
//    public sealed class MetaCloudPayloadMapper : IProviderPayloadMapper
//    {
//        private readonly ILogger<MetaCloudPayloadMapper> _log;

//        public MetaCloudPayloadMapper(ILogger<MetaCloudPayloadMapper> log)
//        {
//            _log = log;
//        }

//        public object BuildPayload(SendPlan plan, RecipientPlan recipient, TemplateEnvelope env)
//        {
//            var to = recipient?.ToPhoneE164 ?? "";
//            _log.LogInformation("MetaMapper: BuildPayload start | template={Template} lang={Lang} to={To}",
//                plan?.TemplateName, plan?.LanguageCode, to);

//            try
//            {
//                var components = BuildComponents(env);

//                var msg = new MetaTemplateMessage(
//                    MessagingProduct: "whatsapp",
//                    To: to,
//                    Type: "template",
//                    Template: new MetaTemplate(
//                        Name: plan.TemplateName,
//                        // Use exactly what you saved in DB (e.g., "en_US")
//                        Language: new MetaLanguage(plan.LanguageCode),
//                        Components: components
//                    )
//                );

//                _log.LogDebug("MetaMapper: Components built | headerKind={HeaderKind} bodyParams={BodyCount} buttons={BtnCount}",
//                    env.HeaderKind, env.BodyParams?.Count ?? 0, env.Buttons?.Count ?? 0);

//                return msg;
//            }
//            catch (Exception ex)
//            {
//                _log.LogError(ex,
//                    "MetaMapper: Failed to build payload | template={Template} lang={Lang} to={To}",
//                    plan?.TemplateName, plan?.LanguageCode, to);
//                throw;
//            }
//        }

//        // Build typed Meta components (header/body/buttons)
//        private IReadOnlyList<MetaComponentBase> BuildComponents(TemplateEnvelope env)
//        {
//            var list = new List<MetaComponentBase>(capacity: 4);

//            // ----- HEADER -----
//            if (env.HeaderKind != HeaderKind.None)
//            {
//                switch (env.HeaderKind)
//                {
//                    case HeaderKind.Text:
//                        if (env.HeaderParams is { Count: > 0 })
//                        {
//                            _log.LogDebug("MetaMapper: Header=text | params={Count}", env.HeaderParams.Count);
//                            list.Add(new MetaHeaderTextComponent(
//                                Type: "header",
//                                Parameters: BuildTextParams(env.HeaderParams, "header")
//                            ));
//                        }
//                        else
//                        {
//                            _log.LogDebug("MetaMapper: Header=text but no params present");
//                        }
//                        break;

//                    case HeaderKind.Image:
//                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
//                        {
//                            _log.LogDebug("MetaMapper: Header=image | url set");
//                            list.Add(new MetaHeaderImageComponent(
//                                Type: "header",
//                                Parameters: new[]
//                                {
//                                    new MetaMediaParam("image", Image: new MetaMediaLink(env.HeaderUrl!))
//                                }
//                            ));
//                        }
//                        else
//                        {
//                            _log.LogWarning("MetaMapper: Header=image but HeaderUrl is missing/empty");
//                        }
//                        break;

//                    case HeaderKind.Video:
//                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
//                        {
//                            _log.LogDebug("MetaMapper: Header=video | url set");
//                            list.Add(new MetaHeaderVideoComponent(
//                                Type: "header",
//                                Parameters: new[]
//                                {
//                                    new MetaMediaParam("video", Video: new MetaMediaLink(env.HeaderUrl!))
//                                }
//                            ));
//                        }
//                        else
//                        {
//                            _log.LogWarning("MetaMapper: Header=video but HeaderUrl is missing/empty");
//                        }
//                        break;

//                    case HeaderKind.Document:
//                        if (!string.IsNullOrWhiteSpace(env.HeaderUrl))
//                        {
//                            _log.LogDebug("MetaMapper: Header=document | url set");
//                            list.Add(new MetaHeaderDocumentComponent(
//                                Type: "header",
//                                Parameters: new[]
//                                {
//                                    new MetaMediaParam("document", Document: new MetaMediaLink(env.HeaderUrl!))
//                                }
//                            ));
//                        }
//                        else
//                        {
//                            _log.LogWarning("MetaMapper: Header=document but HeaderUrl is missing/empty");
//                        }
//                        break;
//                }
//            }

//            // ----- BODY -----
//            if (env.BodyParams is { Count: > 0 })
//            {
//                _log.LogDebug("MetaMapper: Body params={Count}", env.BodyParams.Count);
//                list.Add(new MetaBodyComponent(
//                    Type: "body",
//                    Parameters: BuildTextParams(env.BodyParams, "body")
//                ));
//            }
//            else
//            {
//                _log.LogDebug("MetaMapper: No body params present");
//            }

//            // ----- BUTTONS (URL only) -----
//            var perRecipient = ParsePerRecipient(env.PerRecipientButtonParamsJson);

//            for (int i = 0; i < (env.Buttons?.Count ?? 0) && i < 3; i++)
//            {
//                var b = env.Buttons[i];
//                if (!string.Equals(b.Type, "url", StringComparison.OrdinalIgnoreCase))
//                    continue;

//                var idxStr = i.ToString(); // Meta uses "0" | "1" | "2"
//                var key = $"button{i + 1}.url_param";
//                MetaTextParam[]? parameters = null;

//                if (perRecipient.TryGetValue(key, out var dyn))
//                {
//                    if (string.IsNullOrWhiteSpace(dyn))
//                    {
//                        _log.LogWarning("MetaMapper: URL button param is empty | index={Index} key={Key}", i, key);
//                        // Leave parameters=null; if template actually requires a dynamic value,
//                        // Meta will 400. Better to fail earlier in your validation pipeline if possible.
//                    }
//                    else
//                    {
//                        parameters = new[] { new MetaTextParam("text", dyn) };
//                        _log.LogDebug("MetaMapper: URL button dynamic param set | index={Index} key={Key}", i, key);
//                    }
//                }
//                else
//                {
//                    _log.LogDebug("MetaMapper: URL button has no dynamic param in payload | index={Index} key={Key}", i, key);
//                }

//                list.Add(new MetaButtonUrlComponent(
//                    Type: "button",
//                    SubType: "url",
//                    Index: idxStr,
//                    Parameters: parameters
//                ));
//            }

//            return list;
//        }

//        /// <summary>
//        /// Build Meta "text" parameters ensuring no empty/whitespace values are sent.
//        /// Throws InvalidOperationException with a clear message if any value is missing.
//        /// </summary>
//        private MetaTextParam[] BuildTextParams(IReadOnlyList<string> values, string componentName)
//        {
//            if (values is null || values.Count == 0)
//                return Array.Empty<MetaTextParam>();

//            var result = new List<MetaTextParam>(values.Count);
//            for (int i = 0; i < values.Count; i++)
//            {
//                var v = values[i];
//                if (string.IsNullOrWhiteSpace(v))
//                {
//                    _log.LogWarning("MetaMapper: Empty placeholder value detected | component={Component} index={Index}", componentName, i + 1);
//                    // Fail early instead of hitting Meta with bad payload -> avoids 400 (#131008)
//                    throw new InvalidOperationException(
//                        $"Template {componentName} parameter #{i + 1} is empty/whitespace. " +
//                        $"Provide a value for all placeholders required by the template.");
//                }
//                result.Add(new MetaTextParam("text", v));
//            }
//            return result.ToArray();
//        }

//        private Dictionary<string, string> ParsePerRecipient(string? json)
//        {
//            if (string.IsNullOrWhiteSpace(json))
//                return new();

//            try
//            {
//                return JsonSerializer.Deserialize<Dictionary<string, string>>(json!) ?? new();
//            }
//            catch (Exception ex)
//            {
//                _log.LogError(ex, "MetaMapper: Failed to parse PerRecipientButtonParamsJson. Using empty object.");
//                return new();
//            }
//        }
//    }
//}

