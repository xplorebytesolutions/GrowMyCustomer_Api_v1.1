using xbytechat.api.Features.Audiences.DTOs;

namespace xbytechat.api.Features.Audiences.Services
{
    public interface IAudienceService
    {
        Task<Guid> CreateAsync(Guid businessId, AudienceCreateDto dto, string createdBy);
        Task<List<AudienceSummaryDto>> ListAsync(Guid businessId);
        Task<bool> AssignAsync(Guid businessId, Guid audienceId, AudienceAssignDto dto, string createdBy);
        Task<List<AudienceMemberDto>> GetMembersAsync(Guid businessId, Guid audienceId, int page = 1, int pageSize = 50);
    }
}
