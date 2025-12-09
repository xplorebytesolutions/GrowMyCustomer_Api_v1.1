using xbytechat.api.Features.TemplateModule.DTOs;

namespace xbytechat.api.Features.TemplateModule.Abstractions;

public sealed record TemplateStatusItemDto(
    string LanguageCode,
    string Status,
    string? TemplateId,
    DateTime? UpdatedAt,
    DateTime? LastSyncedAt
);

public interface ITemplateStatusService
{
    /// <summary>
    /// Returns the Meta name derived from the draft key (the one used on submit)
    /// and the per-language rows from WhatsAppTemplates for that business.
    /// </summary>
    Task<(string metaName, IReadOnlyList<TemplateStatusItemDto> items)> GetStatusAsync(
        Guid businessId, Guid draftId, CancellationToken ct = default);
}
