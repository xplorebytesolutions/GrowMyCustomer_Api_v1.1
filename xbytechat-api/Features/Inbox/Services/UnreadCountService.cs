using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Inbox.Services
{
    public class UnreadCountService : IUnreadCountService
    {
        private readonly AppDbContext _db;

        public UnreadCountService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid businessId, Guid userId)
        {
            if (businessId == Guid.Empty || userId == Guid.Empty)
                return new Dictionary<Guid, int>();

            var userReads = _db.ContactReads.AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.UserId == userId);

            var query = _db.MessageLogs.AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.IsIncoming && m.ContactId != null)
                .GroupJoin(
                    userReads,
                    m => m.ContactId,
                    r => r.ContactId,
                    (m, rj) => new { m, rj }
                )
                .SelectMany(x => x.rj.DefaultIfEmpty(), (x, r) => new { x.m, r })
                .Where(x => x.r == null || (x.m.SentAt ?? x.m.CreatedAt) > x.r.LastReadAt)
                .GroupBy(x => x.m.ContactId!.Value)
                .Select(g => new { ContactId = g.Key, Count = g.Count() });

            return await query.ToDictionaryAsync(x => x.ContactId, x => x.Count);
        }
    }
}


//using Microsoft.EntityFrameworkCore;
//using xbytechat.api.Shared;
//using xbytechat.api.Models;

//namespace xbytechat.api.Features.Inbox.Services
//{
//    public class UnreadCountService : IUnreadCountService
//    {
//        private readonly AppDbContext _db;

//        public UnreadCountService(AppDbContext db)
//        {
//            _db = db;
//        }

//        public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid businessId, Guid userId)
//        {
//            // ✅ Load all incoming messages for the business
//            var allMessages = await _db.MessageLogs
//                .Where(m => m.BusinessId == businessId && m.IsIncoming && m.ContactId != null)
//                .ToListAsync();

//            // ✅ Load last read times for this user
//            var contactReads = await _db.ContactReads
//                .Where(r => r.UserId == userId && r.BusinessId == businessId)
//                .ToDictionaryAsync(r => r.ContactId, r => r.LastReadAt);

//            // ✅ Compute unread counts in-memory
//            var unreadCounts = allMessages
//                .GroupBy(m => m.ContactId!.Value)
//                .ToDictionary(
//                    g => g.Key,
//                    g => g.Count(m =>
//                        !contactReads.ContainsKey(g.Key) ||
//                        (m.SentAt ?? m.CreatedAt) > contactReads[g.Key])
//                );

//            return unreadCounts;
//        }
//    }
//}
