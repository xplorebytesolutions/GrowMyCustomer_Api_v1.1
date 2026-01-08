using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.Models;
using xbytechat.api.Features.TemplateModule.Services;
using xbytechat.api.Features.WhatsAppSettings.Models;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateDraftLifecycleService : ITemplateDraftLifecycleService
{
    private readonly AppDbContext _db;
    private readonly IMetaTemplateClient _meta;
    private static readonly Regex MetaNameRx = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public TemplateDraftLifecycleService(AppDbContext db, IMetaTemplateClient meta)
    {
        _db = db;
        _meta = meta;
    }

    public async Task<TemplateDraft> DuplicateDraftAsync(Guid businessId, Guid draftId, string newKey, CancellationToken ct = default)
    {
        var src = await _db.TemplateDrafts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (src is null) throw new KeyNotFoundException("Draft not found.");

        newKey = (newKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newKey))
            throw new ArgumentException("Template name is required.", nameof(newKey));

        if (newKey.Length > 25 || !MetaNameRx.IsMatch(newKey))
            throw new ArgumentException(
                "Invalid template name. Use lowercase letters/numbers/underscores, start with a letter, max 25 characters.",
                nameof(newKey));

        var exists = await _db.TemplateDrafts.AnyAsync(
            x => x.BusinessId == businessId && x.Key == newKey,
            ct);

        if (exists)
            throw new InvalidOperationException("Template name already exists. Please choose a different name.");

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
                HeaderMediaLocalUrl = v.HeaderMediaLocalUrl,
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

        // 2) Fetch variants to delete from Meta
        var variants = await _db.TemplateDraftVariants
            .Where(v => v.TemplateDraftId == draft.Id)
            .ToListAsync(ct);

        foreach (var v in variants)
        {
            // Try to delete from Meta (best effort)
            try
            {
                await _meta.DeleteTemplateAsync(businessId, draft.Key, v.Language, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteDraft] Meta delete warning for {draft.Key}/{v.Language}: {ex.Message}");
            }

            // Also Soft-delete locally in WhatsAppTemplates (synced table)
            var syncRows = await _db.WhatsAppTemplates
                .Where(t => t.BusinessId == businessId && t.Name == draft.Key && t.LanguageCode == v.Language)
                .ToListAsync(ct);

            if (syncRows.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var t in syncRows)
                {
                    t.IsActive = false;
                    t.Status = "DELETED";
                    t.UpdatedAt = now;
                    t.LastSyncedAt = now;
                }
            }
        }

        // 3) Hard-delete variants explicitly
        try
        {
            await _db.TemplateDraftVariants
                .Where(v => v.TemplateDraftId == draft.Id)
                .ExecuteDeleteAsync(ct);
        }
        catch (NotSupportedException)
        {
            if (variants.Count > 0)
                _db.TemplateDraftVariants.RemoveRange(variants);
        }

        // 4) Delete the parent draft
        _db.TemplateDrafts.Remove(draft);

        // 5) Commit
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
