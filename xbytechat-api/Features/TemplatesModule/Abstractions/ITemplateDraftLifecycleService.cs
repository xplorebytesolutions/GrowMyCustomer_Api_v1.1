using xbytechat.api.Features.TemplateModule.Models;

namespace xbytechat.api.Features.TemplateModule.Abstractions;

public interface ITemplateDraftLifecycleService
{
    Task<TemplateDraft> DuplicateDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default);
    Task<bool> DeleteDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default);

    /// <summary>Delete an approved WhatsApp template at Meta and soft-delete it locally.</summary>
    Task<bool> DeleteApprovedTemplateAsync(Guid businessId, string name, string language, CancellationToken ct = default);
}
