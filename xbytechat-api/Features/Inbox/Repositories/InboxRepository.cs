using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Inbox.Repositories
{
    public class InboxRepository : IInboxRepository
    {
        private readonly AppDbContext _context;

        public InboxRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MessageLog>> GetConversationAsync(Guid businessId, string userPhone, string contactPhone, int limit = 50)
        {
            return await _context.MessageLogs
                .Where(m => m.BusinessId == businessId &&
                            ((m.RecipientNumber == contactPhone && m.IsIncoming == false) ||
                             (m.RecipientNumber == userPhone && m.IsIncoming == true)))
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<MessageLog?> GetLastMessageAsync(Guid businessId, string userPhone, string contactPhone)
        {
            return await _context.MessageLogs
                .Where(m => m.BusinessId == businessId &&
                            ((m.RecipientNumber == contactPhone && m.IsIncoming == false) ||
                             (m.RecipientNumber == userPhone && m.IsIncoming == true)))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AddMessageAsync(MessageLog message)
        {
            await _context.MessageLogs.AddAsync(message);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
        public async Task<List<MessageLog>> GetMessagesByContactIdAsync(Guid businessId, Guid contactId)
        {
            return await _context.MessageLogs
                 .Include(m => m.SourceCampaign)
                .Where(m => m.BusinessId == businessId && m.ContactId == contactId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, int>> GetUnreadMessageCountsAsync(Guid businessId)
        {
            return await _context.MessageLogs
                .Where(m => m.BusinessId == businessId &&
                            m.IsIncoming &&
                            m.Status != "Read" &&
                            m.ContactId != null) // ✅ ensure not null
                .GroupBy(m => m.ContactId!.Value) // ✅ safe cast to Guid
                .Select(g => new { ContactId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContactId, x => x.Count);
        }
        public async Task MarkMessagesAsReadAsync(Guid businessId, Guid contactId)
        {
            var unreadMessages = await _context.MessageLogs
                .Where(m => m.BusinessId == businessId &&
                            m.ContactId == contactId &&
                            m.IsIncoming &&
                            m.Status != "Read")
                .ToListAsync(); 
            foreach (var msg in unreadMessages)
                msg.Status = "Read";

            await _context.SaveChangesAsync();
        }
        public async Task<Dictionary<Guid, int>> GetUnreadCountsForUserAsync(Guid businessId, Guid userId)
        {
                    var contactReads = await _context.ContactReads
             .Where(r => r.UserId == userId)
             .ToDictionaryAsync(r => r.ContactId, r => r.LastReadAt);

            // 🟢 Fetch from DB first (no logic yet)
            var allMessages = await _context.MessageLogs
                .Where(m => m.BusinessId == businessId && m.IsIncoming && m.ContactId != null)
                .ToListAsync();

            // 🧠 Now calculate in memory
            var unreadCounts = allMessages
                .GroupBy(m => m.ContactId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(m =>
                        !contactReads.ContainsKey(g.Key) ||
                        (m.SentAt ?? m.CreatedAt) > contactReads[g.Key])
                );


            return unreadCounts;
        }
    }
}
