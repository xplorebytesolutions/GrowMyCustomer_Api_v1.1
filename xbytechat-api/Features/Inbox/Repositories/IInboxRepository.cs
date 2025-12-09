using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Inbox.Repositories
{
    public interface IInboxRepository
    {
        Task<List<MessageLog>> GetConversationAsync(Guid businessId, string userPhone, string contactPhone, int limit = 50);
        Task<MessageLog?> GetLastMessageAsync(Guid businessId, string userPhone, string contactPhone);
        Task AddMessageAsync(MessageLog message);
        Task SaveChangesAsync();
        Task<List<MessageLog>> GetMessagesByContactIdAsync(Guid businessId, Guid contactId);
        Task<Dictionary<Guid, int>> GetUnreadMessageCountsAsync(Guid businessId);
        Task MarkMessagesAsReadAsync(Guid businessId, Guid contactId);
        Task<Dictionary<Guid, int>> GetUnreadCountsForUserAsync(Guid businessId, Guid userId);


    }
}
