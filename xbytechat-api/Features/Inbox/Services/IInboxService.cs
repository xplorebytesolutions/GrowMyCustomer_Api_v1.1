using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.MessageManagement.DTOs;

namespace xbytechat.api.Features.Inbox.Services
{
    public interface IInboxService
    {
        Task<List<MessageLog>> GetConversationAsync(Guid businessId, string userPhone, string contactPhone, int limit = 50);
        Task<MessageLog> SaveIncomingMessageAsync(InboxMessageDto dto);
        Task<MessageLog> SaveOutgoingMessageAsync(InboxMessageDto dto);
        Task<List<MessageLogDto>> GetMessagesByContactAsync(Guid businessId, Guid contactId);
        Task<Dictionary<Guid, int>> GetUnreadMessageCountsAsync(Guid businessId);
        Task MarkMessagesAsReadAsync(Guid businessId, Guid contactId);

        Task<Dictionary<Guid, int>> GetUnreadCountsForUserAsync(Guid businessId, Guid userId);

    }
}
