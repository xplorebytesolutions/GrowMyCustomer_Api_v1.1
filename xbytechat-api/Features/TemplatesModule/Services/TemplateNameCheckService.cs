using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;

namespace xbytechat.api.Features.TemplateModule.Services;

public interface ITemplateNameCheckService
{
    Task<TemplateNameCheckResponse?> CheckAsync(Guid businessId, Guid draftId, string language, CancellationToken ct = default);
}

public sealed class TemplateNameCheckService : ITemplateNameCheckService
{
    private readonly AppDbContext _db;
    private static readonly Regex MetaNameRx = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public TemplateNameCheckService(AppDbContext db) => _db = db;

    public async Task<TemplateNameCheckResponse?> CheckAsync(Guid businessId, Guid draftId, string language, CancellationToken ct = default)
    {
        language = string.IsNullOrWhiteSpace(language) ? "en_US" : language;

        var draft = await _db.TemplateDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

        if (draft is null) return null;

        // Use the user-entered draft Key as the Meta template name (no suffixes / random chars).
        var baseName = (draft.Key ?? string.Empty).Trim();

        // If invalid for Meta, treat as unavailable (no auto-suggestions).
        if (string.IsNullOrWhiteSpace(baseName) || baseName.Length > 25 || !MetaNameRx.IsMatch(baseName))
        {
            return new TemplateNameCheckResponse
            {
                DraftId = draft.Id,
                Language = language,
                Name = baseName,
                Available = false,
                Suggestion = null
            };
        }

        var available = !await ExistsAsync(businessId, baseName, language, ct);
        if (available)
        {
            return new TemplateNameCheckResponse
            {
                DraftId = draft.Id,
                Language = language,
                Name = baseName,
                Available = true,
                Suggestion = null
            };
        }

        return new TemplateNameCheckResponse
        {
            DraftId = draft.Id,
            Language = language,
            Name = baseName,
            Available = false,
            Suggestion = null
        };
    }

    private Task<bool> ExistsAsync(Guid businessId, string name, string language, CancellationToken ct)
        => _db.WhatsAppTemplates.AsNoTracking()
            .AnyAsync(t => t.BusinessId == businessId
                        && t.Name == name
                        && t.LanguageCode == language, ct);

}
