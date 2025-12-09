using xbytechat.api.DTOs.Messages;

namespace xbytechat.api.PayloadBuilders
{
    /// <summary>
    /// Builds payload for sending image messages with media URL.
    /// </summary>
    public class ImageMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(BaseMessageDto dto)
        {
            var imageDto = dto as ImageMessageDto;

            return new
            {
                messaging_product = "whatsapp",
                to = imageDto.RecipientNumber,
                type = "image",
                image = new
                {
                    link = imageDto.MediaUrl
                }
            };
        }
    }
}
