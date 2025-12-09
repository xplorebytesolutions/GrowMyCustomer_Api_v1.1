// 📄 xbytechat-api/Features/ChatInbox/Services/IChatInboxQueryService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ChatInbox.DTOs;

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// Read-only query service for Chat Inbox conversations.
    /// This is a pure "read model" projection over MessageLogs + CRM.
    /// </summary>
    public interface IChatInboxQueryService
    {
        Task<IReadOnlyList<ChatInboxConversationDto>> GetConversationsAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default);
        Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            CancellationToken ct = default);
    }
    
}
