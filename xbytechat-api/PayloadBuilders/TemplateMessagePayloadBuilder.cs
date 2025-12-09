using xbytechat.api.DTOs.Messages;

namespace xbytechat.api.PayloadBuilders
{
    public class TemplateMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(BaseMessageDto dto)
        {
            var templateDto = dto as TemplateMessageDto;
            if (templateDto == null)
                throw new InvalidCastException("DTO is not of type TemplateMessageDto.");

            var components = new List<object>();

            // 🧠 Body parameters
            if (templateDto.TemplateParameters != null && templateDto.TemplateParameters.Any())
            {
                components.Add(new
                {
                    type = "body",
                    parameters = templateDto.TemplateParameters.Select(p => new
                    {
                        type = "text",
                        text = p
                    }).ToList()
                });
            }

            // ✅ Add button placeholders (Meta requires them for static buttons too)
            components.Add(new
            {
                type = "button",
                sub_type = "url",
                index = "0",
                parameters = new object[] { }  // 👈 no parameters if static URL
            });

            components.Add(new
            {
                type = "button",
                sub_type = "phone_number",
                index = "1",
                parameters = new object[] { }  // 👈 no parameters if static phone
            });

            return new
            {
                messaging_product = "whatsapp",
                to = templateDto.RecipientNumber,
                type = "template",
                template = new
                {
                    name = templateDto.TemplateName,
                    language = new
                    {
                        code = templateDto.LanguageCode ?? "en_US"
                    },
                    components
                }
            };
        }
    }
}
