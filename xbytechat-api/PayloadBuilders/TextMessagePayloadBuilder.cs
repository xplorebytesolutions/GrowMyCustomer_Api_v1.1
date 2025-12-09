// File: PayloadBuilders/TextMessagePayloadBuilder.cs
using xbytechat.api.DTOs.Messages;

namespace xbytechat.api.PayloadBuilders
{
    public class TextMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(BaseMessageDto dto)
        {
            var textDto = dto as TextMessageDto;

            if (textDto == null)
                throw new InvalidCastException("DTO is not of type TextMessageDto.");

            return new
            {
                messaging_product = "whatsapp",
                to = textDto.RecipientNumber,
                type = "text",
                text = new
                {
                    body = textDto.MessageContent
                }
            };
        }
    }
}
