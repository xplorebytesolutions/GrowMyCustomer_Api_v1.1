using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.Models;
using xbytechat.api.Features.TemplateModule.Services;
using xbytechat.api.Features.WhatsAppSettings.Models;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateDraftLifecycleService : ITemplateDraftLifecycleService
{
    private readonly AppDbContext _db;
    private readonly IMetaTemplateClient _meta;

    public TemplateDraftLifecycleService(AppDbContext db, IMetaTemplateClient meta)
    {
        _db = db;
        _meta = meta;
    }

    public async Task<TemplateDraft> DuplicateDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        var src = await _db.TemplateDrafts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (src is null) throw new KeyNotFoundException("Draft not found.");

        var keyBase = src.Key;
        var suffix = 2;
        var newKey = $"{keyBase}_copy";
        while (await _db.TemplateDrafts.AnyAsync(x => x.BusinessId == businessId && x.Key == newKey, ct))
        {
            newKey = $"{keyBase}_copy{suffix}";
            suffix++;
        }

        var now = DateTime.UtcNow;
        var dup = new TemplateDraft
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Key = newKey,
            Category = src.Category,
            DefaultLanguage = src.DefaultLanguage,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.TemplateDrafts.Add(dup);

        var variants = await _db.TemplateDraftVariants
            .AsNoTracking()
            .Where(v => v.TemplateDraftId == src.Id)
            .ToListAsync(ct);

        foreach (var v in variants)
        {
            _db.TemplateDraftVariants.Add(new TemplateDraftVariant
            {
                Id = Guid.NewGuid(),
                TemplateDraftId = dup.Id,
                Language = v.Language,
                HeaderType = v.HeaderType,
                HeaderText = v.HeaderText,
                HeaderMediaLocalUrl = v.HeaderMediaLocalUrl, // keep handle if present; user can replace
                BodyText = v.BodyText,
                FooterText = v.FooterText,
                ButtonsJson = v.ButtonsJson,
                ExampleParamsJson = v.ExampleParamsJson,
                IsReadyForSubmission = v.IsReadyForSubmission,
                ValidationErrorsJson = v.ValidationErrorsJson
            });
        }

        await _db.SaveChangesAsync(ct);
        return dup;
    }


    public async Task<bool> DeleteDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        // 1) Find the draft scoped to the tenant
        var draft = await _db.TemplateDrafts
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

        if (draft is null) return false;

        // 2) Hard-delete variants explicitly (no nav needed)

        // If you're on EF Core 7+, this is the fastest way:
        try
        {
            await _db.TemplateDraftVariants
                .Where(v => v.TemplateDraftId == draft.Id)
                .ExecuteDeleteAsync(ct);
        }
        catch (NotSupportedException)
        {
            // Fallback for EF Core < 7: load + RemoveRange
            var children = await _db.TemplateDraftVariants
                .Where(v => v.TemplateDraftId == draft.Id)
                .ToListAsync(ct);
            if (children.Count > 0)
                _db.TemplateDraftVariants.RemoveRange(children);
        }

        // 3) Delete the parent draft
        _db.TemplateDrafts.Remove(draft);

        // 4) Commit
        await _db.SaveChangesAsync(ct);
        return true;
    }


    public async Task<bool> DeleteApprovedTemplateAsync(Guid businessId, string name, string language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Name and language are required.");

        // 1) Ask Meta to delete
        var okMeta = await _meta.DeleteTemplateAsync(businessId, name, language, ct);

        // 2) Soft-delete locally even if Meta already deleted (idempotent UX)
        var rows = await _db.WhatsAppTemplates
            .Where(t => t.BusinessId == businessId && t.Name == name && t.LanguageCode == language)
            .ToListAsync(ct);

        if (rows.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var t in rows)
            {
                t.IsActive = false;
                t.Status = "DELETED"; // keep a conventional marker
                t.UpdatedAt = now;
                t.LastSyncedAt = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        return okMeta;
    }
}
