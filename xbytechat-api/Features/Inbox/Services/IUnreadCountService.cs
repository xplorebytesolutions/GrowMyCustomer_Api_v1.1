using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Inbox.Services
{
    public interface IUnreadCountService
    {
        Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid businessId, Guid userId);
    }
}
