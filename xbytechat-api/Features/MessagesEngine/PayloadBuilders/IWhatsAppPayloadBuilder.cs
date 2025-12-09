using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.PayloadBuilders
{
    public interface IWhatsAppPayloadBuilder
    {
        object BuildPayload(SendMessageDto dto);
    }
}
