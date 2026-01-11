// ✅ Step 1: Final interface
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Helpers;
using System.Threading.Tasks;
using System.IO.Pipelines;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.CampaignModule.DTOs;
using System.Threading;
using xbytechat.api.Features.MessagesEngine.Enums;


namespace xbytechat.api.Features.MessagesEngine.Services
{
    public interface IMessageEngineService
    {

        Task<ResponseResult> SendTemplateMessageAsync(SendMessageDto dto); //
        Task<ResponseResult> SendTextDirectAsync(TextMessageSendDto dto);
        Task<ResponseResult> SendImageDirectAsync(MediaMessageSendDto dto);
        Task<ResponseResult> SendDocumentDirectAsync(MediaMessageSendDto dto);
        Task<ResponseResult> SendVideoDirectAsync(MediaMessageSendDto dto);
        Task<ResponseResult> SendAudioDirectAsync(MediaMessageSendDto dto);
        Task<ResponseResult> SendLocationDirectAsync(LocationMessageSendDto dto);
        Task<ResponseResult> SendAutomationReply(TextMessageSendDto dto);
        Task<ResponseResult> SendTemplateMessageSimpleAsync(Guid businessId, SimpleTemplateMessageDto dto);
        Task<ResponseResult> SendImageCampaignAsync(Guid campaignId, Guid businessId, string triggeredBy);
        Task<ResponseResult> SendImageTemplateMessageAsync(ImageTemplateMessageDto dto, Guid businessId);
            Task<ResponseResult> SendPayloadAsync(
         Guid businessId,
         string provider,          // "PINNACLE" or "META_CLOUD"
         object payload,
         string? phoneNumberId = null);
        Task<ResponseResult> SendVideoTemplateMessageAsync(VideoTemplateMessageDto dto, Guid businessId);
        Task<ResponseResult> SendDocumentTemplateMessageAsync(
           DocumentTemplateMessageDto dto,
           Guid businessId);

        // ✅ NEW: Auto-reply helper (for webhook / AutoReplyBuilder runtime)
        Task<ResponseResult> SendAutoReplyTextAsync(
             Guid businessId,
             string recipientNumber,
             string body,
             CancellationToken ct = default);

        // ✅ NEW overload – AutoReply with explicit DeliveryMode
        Task<ResponseResult> SendAutoReplyTextAsync(
            Guid businessId,
            string recipientNumber,
            string body,
            DeliveryMode mode,
            CancellationToken ct = default);
    }
}
