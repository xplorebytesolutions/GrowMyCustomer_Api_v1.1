using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;
using xbytechat.api.Features.TemplateModule.Utils;

namespace xbytechat.api.Features.TemplateModule.Services;

public interface ITemplateNameCheckService
{
    Task<TemplateNameCheckResponse?> CheckAsync(Guid businessId, Guid draftId, string language, CancellationToken ct = default);
}

public sealed class TemplateNameCheckService : ITemplateNameCheckService
{
    private readonly AppDbContext _db;

    public TemplateNameCheckService(AppDbContext db) => _db = db;

    public async Task<TemplateNameCheckResponse?> CheckAsync(Guid businessId, Guid draftId, string language, CancellationToken ct = default)
    {
        language = string.IsNullOrWhiteSpace(language) ? "en_US" : language;

        var draft = await _db.TemplateDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

        if (draft is null) return null;

        // Compute the Meta template name we use during Submit
        var baseName = MetaNameHelper.FromKey(draft.Key, businessId, MetaNameHelper.ShortBizSuffix(businessId));

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

        // Find first available suggestion by appending _2, _3, ...
        var suggestion = await FirstAvailableAsync(businessId, baseName, language, ct);

        return new TemplateNameCheckResponse
        {
            DraftId = draft.Id,
            Language = language,
            Name = baseName,
            Available = false,
            Suggestion = suggestion
        };
    }

    private Task<bool> ExistsAsync(Guid businessId, string name, string language, CancellationToken ct)
        => _db.WhatsAppTemplates.AsNoTracking()
            .AnyAsync(t => t.BusinessId == businessId
                        && t.Name == name
                        && t.LanguageCode == language, ct);

    private async Task<string> FirstAvailableAsync(Guid businessId, string baseName, string language, CancellationToken ct)
    {
        // avoid unbounded loops; Meta’s name limit is generous, but we keep it sane
        for (int i = 2; i <= 1000; i++)
        {
            var candidate = $"{baseName}_{i}";
            var exists = await ExistsAsync(businessId, candidate, language, ct);
            if (!exists)
                return candidate;
        }
        // fallback: add random short suffix
        return $"{baseName}_{Guid.NewGuid():N}".Substring(0, Math.Min(baseName.Length + 9, 50));
    }
}
