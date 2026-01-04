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
                            m.ContactId != null)
                .GroupBy(m => m.ContactId!.Value)
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

            var allMessages = await _context.MessageLogs
                .Where(m => m.BusinessId == businessId && m.IsIncoming && m.ContactId != null)
                .ToListAsync();

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

        // ✅ Step 4: Soft idempotency lookup (BusinessId + ProviderMessageId/WAMID)
        // Used by InboxService to avoid inserting duplicate inbound rows when Meta retries webhooks.
        public async Task<MessageLog?> FindByProviderMessageIdAsync(Guid businessId, string providerMessageId)
        {
            if (businessId == Guid.Empty) return null;
            if (string.IsNullOrWhiteSpace(providerMessageId)) return null;

            var wamid = providerMessageId.Trim();

            // IMPORTANT:
            // - Use ProviderMessageId only for webhook idempotency.
            // - Do NOT match against MessageId here (prevents cross-path collisions).
            return await _context.MessageLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.BusinessId == businessId &&
                    m.ProviderMessageId != null &&
                    m.ProviderMessageId == wamid
                );
        }
    }
}
