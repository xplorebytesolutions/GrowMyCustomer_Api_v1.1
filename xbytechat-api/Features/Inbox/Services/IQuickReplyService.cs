using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Inbox.Services
{
    public interface IQuickReplyService
    {
        Task<List<QuickReplyDto>> GetAllAsync(Guid businessId, Guid userId,
            string? search = null, bool includeBusiness = true, bool includePersonal = true);

        Task<ResponseResult> CreateAsync(Guid businessId, Guid userId, string actor, QuickReplyCreateDto dto);
        Task<ResponseResult> UpdateAsync(Guid businessId, Guid userId, string actor, Guid id, QuickReplyUpdateDto dto);
        Task<ResponseResult> ToggleActiveAsync(Guid businessId, Guid userId, string actor, Guid id, bool isActive);
        Task<ResponseResult> DeleteAsync(Guid businessId, Guid userId, string actor, Guid id);
    }
}
