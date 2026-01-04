// 📄 xbytechat-api/Features/ChatInbox/Services/IChatInboxQueryService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ChatInbox.DTOs;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public interface IChatInboxQueryService
    {
        // =========================
        // Conversations
        // =========================

        // ✅ Existing (non-paged)
        Task<IReadOnlyList<ChatInboxConversationDto>> GetConversationsAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default);

        // ✅ Cursor paging (used by controller when paged=true)
        Task<PagedResultDto<ChatInboxConversationDto>> GetConversationsPageAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default);

        // =========================
        // Messages (LEGACY)
        // NOTE: These are backward compatible and DO NOT enforce restricted-mode visibility.
        // Prefer the secured overloads below in controllers.
        // =========================

        Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            CancellationToken ct = default);

        Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            CancellationToken ct = default);

        Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            string? cursor,
            CancellationToken ct = default);

        Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByPhoneAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            string? cursor,
            CancellationToken ct = default);

        // =========================
        // Messages (SECURED)
        // ✅ These enforce AssignedOnly restrictions using currentUserId from token.
        // =========================

        Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            Guid currentUserId,
            CancellationToken ct = default);

        Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            Guid currentUserId,
            CancellationToken ct = default);

        Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            string? cursor,
            Guid currentUserId,
            CancellationToken ct = default);

        Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByPhoneAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            string? cursor,
            Guid currentUserId,
            CancellationToken ct = default);
    }
}
