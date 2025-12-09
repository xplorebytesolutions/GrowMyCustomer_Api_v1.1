namespace xbytechat.api.PayloadBuilders
{
    using xbytechat.api.DTOs.Messages;

    public interface IWhatsAppPayloadBuilder
    {
        object BuildPayload(BaseMessageDto dto);
    }
}
