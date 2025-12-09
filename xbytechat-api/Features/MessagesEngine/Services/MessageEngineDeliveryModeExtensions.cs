// 📄 xbytechat-api/Features/MessagesEngine/Services/MessageEngineDeliveryModeExtensions.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Enums;
using xbytechat.api.Helpers; // ResponseResult

namespace xbytechat.api.Features.MessagesEngine.Services
{
    /// <summary>
    /// Extension methods that add DeliveryMode-aware overloads
    /// on top of the existing IMessageEngineService interface.
    ///
    /// v1 behaviour:
    /// - These methods mostly delegate to the original overloads.
    /// - For templates, we now copy the DeliveryMode onto the DTO.
    /// - The core MessageEngineService can later read dto.DeliveryMode
    ///   and decide whether to use the Outbox or send immediately.
    /// </summary>
    public static class MessageEngineDeliveryModeExtensions
    {
        /// <summary>
        /// DeliveryMode-aware overload for sending simple template messages.
        /// Currently sets dto.DeliveryMode = mode, then calls the existing implementation.
        /// </summary>
        public static Task<ResponseResult> SendTemplateMessageSimpleAsync(
            this IMessageEngineService engine,
            Guid businessId,
            SimpleTemplateMessageDto dto,
            DeliveryMode mode,
            CancellationToken ct = default)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));
            if (dto is null) throw new ArgumentNullException(nameof(dto));

            // 🆕 Stamp the requested mode onto the DTO so the engine can inspect it.
            dto.DeliveryMode = mode;

            // v1: underlying implementation still behaves the same.
            return engine.SendTemplateMessageSimpleAsync(businessId, dto);
        }

        /// <summary>
        /// DeliveryMode-aware overload for auto-reply text messages.
        /// For now, the mode is ignored and we delegate to the existing method.
        /// Later we can teach the engine to branch on mode here as well.
        /// </summary>
        public static Task<ResponseResult> SendAutoReplyTextAsync(
            this IMessageEngineService engine,
            Guid businessId,
            string recipientNumber,
            string body,
            DeliveryMode mode,
            CancellationToken ct = default)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));

            // v1: keep existing behaviour for text; still uses the current pipeline.
            return engine.SendAutoReplyTextAsync(businessId, recipientNumber, body, ct);
        }
    }
}
