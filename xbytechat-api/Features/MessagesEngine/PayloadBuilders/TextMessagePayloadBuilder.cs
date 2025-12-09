using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public class TextMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(SendMessageDto dto)
        {
            return new
            {
                messaging_product = "whatsapp",
                to = dto.RecipientNumber,
                type = "text",
                text = new
                {
                    body = dto.TextContent
                }
            };
        }
    }
}
