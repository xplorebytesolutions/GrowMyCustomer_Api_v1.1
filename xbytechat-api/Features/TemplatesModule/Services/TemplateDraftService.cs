using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;
using xbytechat.api.Features.TemplateModule.Payload;
using xbytechat.api.Features.TemplateModule.Validators;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateDraftService : ITemplateDraftService
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public TemplateDraftService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TemplateDraft> CreateDraftAsync(Guid businessId, TemplateDraftCreateDto dto, CancellationToken ct = default)
    {
        var exists = await _db.TemplateDrafts
            .AnyAsync(x => x.BusinessId == businessId && x.Key == dto.Key, ct);
        if (exists)
            throw new InvalidOperationException("A draft with the same Key already exists for this business.");

        var draft = new TemplateDraft
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Key = dto.Key.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "UTILITY" : dto.Category.Trim().ToUpperInvariant(),
            DefaultLanguage = string.IsNullOrWhiteSpace(dto.DefaultLanguage) ? "en_US" : dto.DefaultLanguage.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TemplateDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<TemplateDraftVariant> UpsertVariantAsync(Guid businessId, Guid draftId, TemplateDraftVariantUpsertDto dto, CancellationToken ct = default)
    {
        var draft = await _db.TemplateDrafts
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

        if (draft is null)
            throw new KeyNotFoundException("Draft not found for this business.");

        var validation = VariantValidator.Validate(dto);
        if (!validation.Ok)
            throw new InvalidOperationException(string.Join(" | ", validation.Errors));

        var lang = dto.Language.Trim();

        var variant = await _db.TemplateDraftVariants
            .FirstOrDefaultAsync(v => v.TemplateDraftId == draft.Id && v.Language == lang, ct);

        var buttonsJson = JsonSerializer.Serialize(dto.Buttons ?? new List<ButtonDto>(), JsonOpts);
        var examplesJson = JsonSerializer.Serialize(dto.Examples ?? new Dictionary<string, string>(), JsonOpts);

        if (variant is null)
        {
            variant = new TemplateDraftVariant
            {
                Id = Guid.NewGuid(),
                TemplateDraftId = draft.Id,
                Language = lang,
                BodyText = dto.BodyText,
                HeaderType = dto.HeaderType,
                HeaderText = dto.HeaderText,
                HeaderMediaLocalUrl = dto.HeaderMediaLocalUrl,
                FooterText = dto.FooterText,
                ButtonsJson = buttonsJson,
                ExampleParamsJson = examplesJson,
                IsReadyForSubmission = true,
                ValidationErrorsJson = null
            };
            _db.TemplateDraftVariants.Add(variant);
        }
        else
        {
            variant.BodyText = dto.BodyText;
            variant.HeaderType = dto.HeaderType;
            variant.HeaderText = dto.HeaderText;
            variant.HeaderMediaLocalUrl = dto.HeaderMediaLocalUrl;
            variant.FooterText = dto.FooterText;
            variant.ButtonsJson = buttonsJson;
            variant.ExampleParamsJson = examplesJson;
            variant.IsReadyForSubmission = true;
            variant.ValidationErrorsJson = null;
        }

        draft.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return variant;
    }

    public async Task<bool> ValidateAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        var draft = await _db.TemplateDrafts
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (draft is null) return false;

        var variants = await _db.TemplateDraftVariants
            .Where(v => v.TemplateDraftId == draft.Id)
            .ToListAsync(ct);

        return variants.Any(v => v.IsReadyForSubmission);
    }

    public async Task<(bool ok, Dictionary<string, List<string>> errors)> ValidateAllAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        var draft = await _db.TemplateDrafts
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (draft is null) return (false, new() { { "global", new() { "Draft not found." } } });

        var variants = await _db.TemplateDraftVariants
            .Where(v => v.TemplateDraftId == draft.Id)
            .ToListAsync(ct);

        if (variants.Count == 0)
            return (false, new() { { "global", new() { "No language variants found." } } });

        // Map DB rows to validator view
        var views = variants.Select(v => new MultiLanguageValidator.VariantView
        {
            Language = v.Language,
            BodyText = v.BodyText,
            HeaderType = v.HeaderType,
            HeaderText = v.HeaderText,
            HeaderMediaLocalUrl = v.HeaderMediaLocalUrl,
            FooterText = v.FooterText,
            Buttons = SafeDeserialize<List<ButtonDto>>(v.ButtonsJson) ?? new(),
            Examples = SafeDeserialize<Dictionary<string, string>>(v.ExampleParamsJson) ?? new()
        }).ToList();

        var (ok, errors) = MultiLanguageValidator.Validate(views);

        // Persist per-variant readiness & last validation errors (optional but helpful)
        foreach (var v in variants)
        {
            v.IsReadyForSubmission = !errors.ContainsKey(v.Language) || errors[v.Language].Count == 0;
            v.ValidationErrorsJson = errors.ContainsKey(v.Language)
                ? JsonSerializer.Serialize(errors[v.Language], JsonOpts)
                : null;
        }
        await _db.SaveChangesAsync(ct);

        return (ok, errors);
    }

    public Task<TemplateDraft?> GetDraftAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
        => _db.TemplateDrafts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);

    public async Task<IReadOnlyList<TemplateDraft>> ListDraftsAsync(Guid businessId, CancellationToken ct = default)
    {
        var list = await _db.TemplateDrafts.AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return list;
    }

    private static T? SafeDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json!, JsonOpts); } catch { return default; }
    }

    public async Task<bool> SetHeaderHandleAsync(
    Guid businessId,
    Guid draftId,
    string language,
    string mediaType,
    string assetHandle,
    CancellationToken ct = default)
    {
        var draft = await _db.TemplateDrafts
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (draft is null) return false;

        var lang = language.Trim();
        var variant = await _db.TemplateDraftVariants
            .FirstOrDefaultAsync(v => v.TemplateDraftId == draft.Id && v.Language == lang, ct);

        if (variant is null)
        {
            // create the variant if it doesn't exist yet (handy UX)
            variant = new TemplateDraftVariant
            {
                Id = Guid.NewGuid(),
                TemplateDraftId = draft.Id,
                Language = lang,
                BodyText = string.Empty,
                HeaderType = mediaType.ToUpperInvariant(),
                HeaderText = null,
                HeaderMediaLocalUrl = $"handle:{assetHandle}",
                FooterText = null,
                ButtonsJson = "[]",
                ExampleParamsJson = "{}",
                IsReadyForSubmission = false,
                ValidationErrorsJson = null
            };
            _db.TemplateDraftVariants.Add(variant);
        }
        else
        {
            variant.HeaderType = mediaType.ToUpperInvariant(); // IMAGE|VIDEO|DOCUMENT
            variant.HeaderMediaLocalUrl = $"handle:{assetHandle}";
            // don't force IsReadyForSubmission here; Validate/Submit will compute it
        }

        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
    // xbytechat-api/Features/TemplateModule/Services/TemplateDraftService.cs
    // (inside the class; no local SafeDeserialize here — reuse your existing one)


    public async Task<TemplatePreviewDto?> GetPreviewAsync(
        Guid businessId, Guid draftId, string language, CancellationToken ct = default)
    {
        var draft = await _db.TemplateDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (draft is null) return null;

        var variant = await _db.TemplateDraftVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.TemplateDraftId == draft.Id && v.Language == language, ct);
        if (variant is null) return null;

        // Reuse your existing SafeDeserialize<T>(json, JsonOpts)
        var buttons = SafeDeserialize<List<ButtonDto>>(variant.ButtonsJson) ?? new();
        var examples = SafeDeserialize<Dictionary<string, string>>(variant.ExampleParamsJson) ?? new();

        // Preview: no binary fetch; just mark media header types
        string? headerHandleOrNull = null;

        var (components, examplesPayload) = MetaComponentsBuilder.Build(
            variant.HeaderType,
            variant.HeaderText,
            headerHandleOrNull,
            variant.BodyText,
            variant.FooterText,
            buttons,
            examples
        );

        string ResolveVars(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var result = s;
            foreach (var kv in examples)
                result = result.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.Ordinal);
            return result;
        }

        string headerPreview = variant.HeaderType?.ToUpperInvariant() switch
        {
            "TEXT" => ResolveVars(variant.HeaderText ?? string.Empty),
            "IMAGE" => "[IMAGE HEADER]",
            "VIDEO" => "[VIDEO HEADER]",
            "DOCUMENT" => "[DOCUMENT HEADER]",
            _ => string.Empty
        };

        var buttonLabels = new List<string>();
        foreach (var b in buttons)
        {
            if (string.Equals(b.Type, "QUICK_REPLY", StringComparison.OrdinalIgnoreCase))
                buttonLabels.Add($"[Quick Reply] {b.Text}");
            else if (string.Equals(b.Type, "URL", StringComparison.OrdinalIgnoreCase))
                buttonLabels.Add($"[URL] {b.Text}");
            else if (string.Equals(b.Type, "PHONE", StringComparison.OrdinalIgnoreCase))
                buttonLabels.Add($"[Phone] {b.Text}");
        }

        return new TemplatePreviewDto
        {
            Language = language,
            Header = headerPreview,
            Body = ResolveVars(variant.BodyText ?? string.Empty),
            Footer = ResolveVars(variant.FooterText ?? string.Empty),
            Buttons = buttonLabels,
            ComponentsPayload = components,
            ExamplesPayload = examplesPayload
        };
    }

    // Keep your SafeDeserialize helper in this class:

}
