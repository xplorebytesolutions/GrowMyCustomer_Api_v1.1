using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public class CtaMessagePayloadBuilder : IWhatsAppPayloadBuilder
    {
        public object BuildPayload(SendMessageDto dto)
        {
            return new
            {
                messaging_product = "whatsapp",
                to = dto.RecipientNumber,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = dto.TextContent },
                    action = new
                    {
                        buttons = dto.CtaButtons?.Select(b => new
                        {
                            type = "reply",
                            reply = new
                            {
                                id = b.Value,
                                title = b.Title
                            }
                        }).ToList()
                    }
                }
            };
        }
    }
}
