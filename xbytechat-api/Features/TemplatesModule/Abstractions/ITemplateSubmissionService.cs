using xbytechat.api.Features.TemplateModule.DTOs;

namespace xbytechat.api.Features.TemplateModule.Abstractions;

public interface ITemplateSubmissionService
{
    Task<TemplateSubmitResponseDto> SubmitAsync(Guid businessId, Guid draftId, CancellationToken ct = default);
    Task<int> SyncStatusAsync(Guid businessId, CancellationToken ct = default);
}
