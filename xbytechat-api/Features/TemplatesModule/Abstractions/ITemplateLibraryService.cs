using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;

namespace xbytechat.api.Features.TemplateModule.Abstractions;

public interface ITemplateLibraryService
{
    Task<IReadOnlyList<TemplateLibraryItem>> ListAsync(string? industry, CancellationToken ct = default);
    Task<TemplateDraft> InstantiateDraftAsync(Guid businessId, Guid libraryItemId, IEnumerable<string> languages, CancellationToken ct = default);
    Task<(TemplateLibraryItem item, List<TemplateLibraryVariant> variants)> GetItemAsync(
               Guid libraryItemId,
               CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListIndustriesAsync(CancellationToken ct = default);
    //Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, CancellationToken ct = default);
    Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, bool dryRun = false, CancellationToken ct = default);
    Task<TemplateLibraryListResponse> SearchAsync(
        string? industry, string? q, string? sort, int page, int pageSize, CancellationToken ct = default);
    Task<LibraryImportRequest> ExportAsync(string? industry, CancellationToken ct = default);
}
