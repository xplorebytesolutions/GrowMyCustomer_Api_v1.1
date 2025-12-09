// Features/MessagesEngine/PayloadBuilders/MessagePayloadBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public static class MessagePayloadBuilder
    {
        // New: a single canonical builder that understands our CSV-materialized shapes.
        public static object BuildTemplatePayload(
            string toPhoneE164,
            string templateName,
            string languageCode,
            string headerType,                 // "none"|"text"|"image"|"video"|"document"
            string? headerMediaUrl,            // for image/video/document
            IReadOnlyList<string> bodyParams,  // {{1}}..{{N}}
            IReadOnlyList<string>? headerTextParams, // header text {{n}} if headerType=="text"
            IReadOnlyDictionary<string, string>? buttonUrlParams // keys: "button1.url_param".."button3.url_param"
        )
        {
            var components = new List<object>();

            // 1) HEADER
            switch ((headerType ?? "none").ToLowerInvariant())
            {
                case "text":
                    if (headerTextParams != null && headerTextParams.Count > 0)
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = headerTextParams.Select(v => new { type = "text", text = v ?? string.Empty }).ToArray()
                        });
                    }
                    break;

                case "image":
                    if (!string.IsNullOrWhiteSpace(headerMediaUrl))
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "image", image = new { link = headerMediaUrl } }
                            }
                        });
                    }
                    break;

                case "video":
                    if (!string.IsNullOrWhiteSpace(headerMediaUrl))
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "video", video = new { link = headerMediaUrl } }
                            }
                        });
                    }
                    break;

                case "document":
                    if (!string.IsNullOrWhiteSpace(headerMediaUrl))
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "document", document = new { link = headerMediaUrl } }
                            }
                        });
                    }
                    break;
            }

            // 2) BODY
            if (bodyParams != null && bodyParams.Count > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = bodyParams.Select(v => new { type = "text", text = v ?? string.Empty }).ToArray()
                });
            }

            // 3) BUTTONS (URL dynamic only)
            // Expect buttonUrlParams: button{1..3}.url_param -> string
            if (buttonUrlParams != null && buttonUrlParams.Count > 0)
            {
                var buttons = new List<object>();

                for (var pos = 1; pos <= 3; pos++)
                {
                    var key = $"button{pos}.url_param";
                    if (buttonUrlParams.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        buttons.Add(new
                        {
                            type = "button",
                            sub_type = "url",
                            index = pos - 1, // Meta expects 0-based index
                            parameters = new object[]
                            {
                                new { type = "text", text = val }
                            }
                        });
                    }
                }

                if (buttons.Count > 0)
                {
                    components.AddRange(buttons);
                }
            }

            // Meta/Pinnacle style template envelope
            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhoneE164,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = languageCode }, // << not hardcoded
                    components = components.ToArray()
                }
            };

            return payload;
        }
    }
}


//using xbytechat.api.Features.CampaignModule.Models;
//using xbytechat.api.Shared.utility;

//namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
//{
//    public static class MessagePayloadBuilder
//    {
//        /// <summary>
//        /// Builds a WhatsApp template message payload for image header + buttons.
//        /// </summary>
//        public static object BuildImageTemplatePayload(
//            string templateName,
//            string languageCode,
//            string recipientNumber,
//            List<string> templateParams,
//            string? imageUrl,
//            List<CampaignButton>? buttons
//        )
//        {
//            var components = new List<object>();

//            // ✅ Body with template params
//            if (templateParams != null && templateParams.Any())
//            {
//                components.Add(new
//                {
//                    type = "body",
//                    parameters = templateParams.Select(p => new { type = "text", text = p }).ToArray()
//                });
//            }

//            // ✅ Header image if present
//            if (!string.IsNullOrWhiteSpace(imageUrl))
//            {
//                components.Add(new
//                {
//                    type = "header",
//                    parameters = new[]
//                    {
//                    new { type = "image", image = new { link = imageUrl } }
//                }
//                });
//            }

//            // ✅ CTA buttons
//            if (buttons != null && buttons.Any())
//            {
//                var buttonComponents = buttons
//                    .OrderBy(b => b.Position)
//                    .Take(3)
//                    .Select((btn, index) => new
//                    {
//                        type = "button",
//                        sub_type = btn.Type, // "url" or "phone_number"
//                        index = index.ToString(),
//                        parameters = new[]
//                        {
//                        new { type = "text", text = btn.Value }
//                        }
//                    });

//                components.AddRange(buttonComponents);
//            }

//            // ✅ Final WhatsApp Template Payload
//            return new
//            {
//                messaging_product = "whatsapp",
//                to = recipientNumber,
//                type = "template",
//                template = new
//                {
//                    name = templateName,
//                    language = new { code = languageCode },
//                    components = components
//                }
//            };
//        }
//    }

//}