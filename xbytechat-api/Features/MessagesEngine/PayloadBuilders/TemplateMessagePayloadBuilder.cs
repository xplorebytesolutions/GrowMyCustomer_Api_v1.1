using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public class TemplateMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(SendMessageDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.TemplateName))
                throw new ArgumentException("TemplateName is required.");
            if (dto.TemplateParameters == null || dto.TemplateParameters.Count == 0)
                throw new ArgumentException("TemplateParameters are required for template messages.");

            // Sort by placeholder index like {{1}}, {{2}}, guarding against bad keys
            var bodyParams = dto.TemplateParameters
                .Select(kvp =>
                {
                    var key = kvp.Key?.Trim('{', '}');
                    _ = int.TryParse(key, out var idx);
                    return (idx, kvp.Value);
                })
                .OrderBy(t => t.idx)
                .Select(t => new { type = "text", text = t.Value })
                .ToArray();

            var components = new List<object>
            {
                new { type = "body", parameters = bodyParams }
            };

            if (dto.ButtonParams != null && dto.ButtonParams.Any())
            {
                for (int i = 0; i < dto.ButtonParams.Count; i++)
                {
                    components.Add(new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i.ToString(),
                        parameters = new[]
                        {
                            new { type = "text", text = dto.ButtonParams[i] }
                        }
                    });
                }
            }

            return new
            {
                messaging_product = "whatsapp",
                to = dto.RecipientNumber,
                type = "template",
                template = new
                {
                    name = dto.TemplateName,
                    language = new { code = "en_US" },
                    components
                }
            };
        }
    }
}


//using xbytechat.api.Features.MessagesEngine.DTOs;
//using xbytechat.api.Helpers;

//namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
//{
//    public class TemplateMessagePayloadBuilder : IWhatsAppPayloadBuilder
//    {
//        public object BuildPayload(SendMessageDto dto)
//        {
//            var components = new List<object>();

//            // ✅ BODY PARAMETERS: Insert dynamic values into the template body
//            // WhatsApp expects these to be in order ({{1}}, {{2}}, etc.)
//            if (dto.TemplateParameters == null || dto.TemplateParameters.Count == 0)
//                return ResponseResult.ErrorInfo("❌ Missing template parameters.");
//            if (dto.TemplateParameters != null && dto.TemplateParameters.Any())
//                {
//                var bodyParams = dto.TemplateParameters
//                    .OrderBy(kvp => int.Parse(kvp.Key.Trim('{', '}'))) // 🔢 Extract and sort by index
//                    .Select(kvp => new
//                    {
//                        type = "text",
//                        text = kvp.Value
//                    }).ToArray();

//                components.Add(new
//                {
//                    type = "body",
//                    parameters = bodyParams
//                });
//            }

//            // ✅ BUTTON PARAMETERS: For templates with dynamic URL buttons (index-based)
//            if (dto.ButtonParams != null && dto.ButtonParams.Any())
//            {
//                for (int i = 0; i < dto.ButtonParams.Count; i++)
//                {
//                    components.Add(new
//                    {
//                        type = "button",
//                        sub_type = "url",
//                        index = i.ToString(), // WhatsApp requires index as a string
//                        parameters = new[]
//                        {
//                            new
//                            {
//                                type = "text",
//                                text = dto.ButtonParams[i]
//                            }
//                        }
//                    });
//                }
//            }

//            // ✅ FINAL WHATSAPP TEMPLATE PAYLOAD
//            var payload = new
//            {
//                messaging_product = "whatsapp",
//                to = dto.RecipientNumber,
//                type = "template",
//                template = new
//                {
//                    name = dto.TemplateName,
//                    language = new { code = "en_US" },
//                    components = components
//                }
//            };

//            // 🪵 Debug log for developer console (optional)
//            Console.WriteLine("📦 Built WhatsApp Template Payload:");
//            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
//            {
//                WriteIndented = true
//            }));

//            return payload;
//        }
//    }
//}
