using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public class ImageMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(SendMessageDto dto)
        {
            return new
            {
                messaging_product = "whatsapp",
                to = dto.RecipientNumber,
                type = "image",
                image = new
                {
                    link = dto.MediaUrl
                }
            };
        }
    }
}
