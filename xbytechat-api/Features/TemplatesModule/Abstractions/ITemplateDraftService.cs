using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;

namespace xbytechat.api.Features.TemplateModule.Abstractions;

public interface ITemplateDraftService
{
    Task<TemplateDraft> CreateDraftAsync(Guid businessId, TemplateDraftCreateDto dto, CancellationToken ct = default);
    Task<TemplateDraftVariant> UpsertVariantAsync(Guid businessId, Guid draftId, TemplateDraftVariantUpsertDto dto, CancellationToken ct = default);
    Task<bool> ValidateAsync(Guid businessId, Guid draftId, CancellationToken ct = default);
    Task<TemplateDraft?> GetDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDraft>> ListDraftsAsync(Guid businessId, CancellationToken ct = default);
    Task<(bool ok, Dictionary<string, List<string>> errors)> ValidateAllAsync(Guid businessId, Guid draftId, CancellationToken ct = default);
    Task<bool> SetHeaderHandleAsync(
    Guid businessId,
    Guid draftId,
    string language,
    string mediaType,          // IMAGE | VIDEO | DOCUMENT
    string assetHandle,        // raw handle without "handle:" prefix
    CancellationToken ct = default);
    Task<TemplatePreviewDto?> GetPreviewAsync(
        Guid businessId, Guid draftId, string language, CancellationToken ct = default);
}
