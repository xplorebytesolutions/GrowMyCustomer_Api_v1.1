using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.Utils;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateStatusService : ITemplateStatusService
{
    private readonly AppDbContext _db;

    public TemplateStatusService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(string metaName, IReadOnlyList<TemplateStatusItemDto> items)> GetStatusAsync(
        Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        // Load draft (to get Key for Meta name derivation)
        var draft = await _db.TemplateDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

        if (draft is null)
            throw new KeyNotFoundException("Draft not found.");

        var name = MetaNameHelper.FromKey(draft.Key, businessId, MetaNameHelper.ShortBizSuffix(businessId));

        // Query your SoT table
        var rows = await _db.WhatsAppTemplates
            .AsNoTracking()
            .Where(t => t.BusinessId == businessId && t.Name == name)
            .OrderBy(t => t.LanguageCode)
            .Select(t => new TemplateStatusItemDto(
                t.LanguageCode,
                t.Status ?? string.Empty,
                t.TemplateId,
                t.UpdatedAt,
                t.LastSyncedAt
            ))
            .ToListAsync(ct);

        return (name, rows);
    }
}
