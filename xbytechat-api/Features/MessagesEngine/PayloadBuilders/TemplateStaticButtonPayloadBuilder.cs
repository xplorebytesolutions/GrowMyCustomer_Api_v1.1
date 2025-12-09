using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public class TemplateStaticButtonPayloadBuilder
    {
        public static object Build(SendTemplateMessageSimpleDto dto)
        {
            var components = new List<object>();

            // ✅ Add Body Params
            if (dto.TemplateParameters != null && dto.TemplateParameters.Any())
            {
                components.Add(new
                {
                    type = "body",
                    parameters = dto.TemplateParameters.Select(p => new
                    {
                        type = "text",
                        text = p
                    }).ToArray()
                });
            }

            // ⚠️ DO NOT add button components for static buttons
            // Meta will render them automatically if template has static buttons defined
            // You can later add logic here for dynamic buttons if needed

            return new
            {
                messaging_product = "whatsapp",
                to = dto.RecipientNumber,
                type = "template",
                template = new
                {
                    name = dto.TemplateName,
                    language = new { code = "en_US" },
                    components = components
                }
            };
        }
    }
}

