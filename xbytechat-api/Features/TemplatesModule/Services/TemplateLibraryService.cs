using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Models;
using System.Text.Json;
namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateLibraryService : ITemplateLibraryService
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public TemplateLibraryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TemplateLibraryItem>> ListAsync(string? industry, CancellationToken ct = default)
    {
        var q = _db.TemplateLibraryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(industry))
        {
            var ind = industry.Trim().ToUpperInvariant();
            q = q.Where(x => x.Industry == ind);
        }

        return await q
            .OrderByDescending(x => x.IsFeatured)
            .ThenBy(x => x.Industry)
            .ThenBy(x => x.Key)
            .ToListAsync(ct);
    }

    public async Task<TemplateDraft> InstantiateDraftAsync(
      Guid businessId,
      Guid libraryItemId,
      IEnumerable<string> languages,
      CancellationToken ct = default)
    {
        var item = await _db.TemplateLibraryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == libraryItemId, ct);

        if (item is null)
            throw new KeyNotFoundException("Library item not found.");

        // ---- Normalize incoming languages ----
        var requested = (languages ?? Array.Empty<string>())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToArray();

        // Load all variants for this item once
        var allLibVariants = await _db.TemplateLibraryVariants
            .AsNoTracking()
            .Where(v => v.LibraryItemId == item.Id)
            .ToListAsync(ct);

        if (allLibVariants.Count == 0)
            throw new InvalidOperationException("Selected library item has no language variants.");

        // If "ALL" is requested (case-insensitive), use all languages available
        bool wantsAll = requested.Any(l => string.Equals(l, "ALL", StringComparison.OrdinalIgnoreCase));
        var targetLangs = wantsAll
            ? allLibVariants.Select(v => v.Language).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : requested;

        // Validate: ensure at least one requested language exists
        var availableLangs = new HashSet<string>(
            allLibVariants.Select(v => v.Language),
            StringComparer.OrdinalIgnoreCase);

        var matchedLangs = targetLangs.Where(l => availableLangs.Contains(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (matchedLangs.Length == 0)
            throw new InvalidOperationException(
                $"None of the requested languages exist for this template. " +
                $"Requested: [{string.Join(", ", targetLangs)}]; " +
                $"Available: [{string.Join(", ", availableLangs)}]");

        // ---- Make a unique key for the business if needed ----
        var key = item.Key;
        var suffix = 1;
        while (await _db.TemplateDrafts.AnyAsync(x => x.BusinessId == businessId && x.Key == key, ct))
        {
            suffix++;
            key = $"{item.Key}_{suffix}";
        }

        // ---- Create draft ----
        var defaultLang = matchedLangs[0];
        var draft = new TemplateDraft
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Key = key,
            Category = item.Category,
            DefaultLanguage = defaultLang,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.TemplateDrafts.Add(draft);

        // ---- Clone only the matched languages ----
        var libVariants = allLibVariants
            .Where(v => matchedLangs.Contains(v.Language, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var lv in libVariants)
        {
            var variant = new TemplateDraftVariant
            {
                Id = Guid.NewGuid(),
                TemplateDraftId = draft.Id,
                Language = lv.Language,
                BodyText = lv.BodyText,
                HeaderType = lv.HeaderType,
                HeaderText = lv.HeaderText,
                HeaderMediaLocalUrl = null,         // Library media is preview-only; users upload their own
                FooterText = lv.FooterText,
                ButtonsJson = lv.ButtonsJson ?? "[]",
                // IMPORTANT: examples are a map/object -> "{}" by default
                ExampleParamsJson = lv.ExampleParamsJson ?? "{}",
                IsReadyForSubmission = true,
                ValidationErrorsJson = null
            };
            _db.TemplateDraftVariants.Add(variant);
        }

        await _db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<(TemplateLibraryItem item, List<TemplateLibraryVariant> variants)> GetItemAsync(
    Guid libraryItemId,
    CancellationToken ct = default)
    {
        var item = await _db.TemplateLibraryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == libraryItemId, ct);

        if (item is null)
            throw new KeyNotFoundException("Library item not found.");

        var variants = await _db.TemplateLibraryVariants
            .AsNoTracking()
            .Where(v => v.LibraryItemId == item.Id)
            .OrderBy(v => v.Language)
            .ToListAsync(ct);

        return (item, variants);
    }
    public async Task<IReadOnlyList<string>> ListIndustriesAsync(CancellationToken ct = default)
    {
        var raw = await _db.TemplateLibraryItems
            .AsNoTracking()
            .Select(x => x.Industry)
            .Where(x => x != null && x != "")
            .ToListAsync(ct);

        // Normalize to upper (your storage uses UPPER), then distinct & sort for UX
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in raw)
        {
            var v = s!.Trim();
            if (v.Length == 0) continue;
            set.Add(v.ToUpperInvariant());
        }
        return set.OrderBy(x => x).ToList();
    }
    //public async Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, CancellationToken ct = default)
    public async Task<LibraryImportResult> ImportAsync(LibraryImportRequest request, bool dryRun = false, CancellationToken ct = default)
    {
        var res = new LibraryImportResult();
        if (request?.Items == null || request.Items.Count == 0)
        {
            res.Success = true;
            return res;
        }

        // reasonable safety cap; adjust if needed
        if (request.Items.Count > 1000)
            throw new InvalidOperationException("Too many items in one import. Split into smaller batches (≤1000).");

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };

        string NormalizeCategory(string cat)
        {
            var c = (cat ?? "").Trim().ToUpperInvariant();
            return c switch
            {
                "UTILITY" or "MARKETING" or "AUTHENTICATION" => c,
                _ => throw new InvalidOperationException("category must be one of UTILITY, MARKETING, AUTHENTICATION")
            };
        }

        string NormalizeHeaderType(string? ht)
        {
            var t = (ht ?? "NONE").Trim().ToUpperInvariant();
            return t switch
            {
                "NONE" or "TEXT" or "IMAGE" or "VIDEO" or "DOCUMENT" => t,
                _ => throw new InvalidOperationException("headerType must be NONE|TEXT|IMAGE|VIDEO|DOCUMENT")
            };
        }

        string ButtonsToJson(List<LibraryImportButton> buttons)
        {
            // light validation: max 3; text required; type-specific fields
            if (buttons is { Count: > 3 })
                throw new InvalidOperationException("A variant cannot have more than 3 buttons.");

            foreach (var b in buttons)
            {
                if (string.IsNullOrWhiteSpace(b.Type)) throw new InvalidOperationException("button.type is required.");
                if (string.IsNullOrWhiteSpace(b.Text)) throw new InvalidOperationException("button.text is required.");

                var t = b.Type.Trim().ToUpperInvariant();
                if (t == "URL" && string.IsNullOrWhiteSpace(b.Url))
                    throw new InvalidOperationException("URL button requires url.");
                if (t == "PHONE" && string.IsNullOrWhiteSpace(b.Phone))
                    throw new InvalidOperationException("PHONE button requires phone.");
            }

            return JsonSerializer.Serialize(buttons ?? new(), jsonOpts);
        }

        string ExamplesToJson(Dictionary<string, string>? map)
            => JsonSerializer.Serialize(map ?? new Dictionary<string, string>(), jsonOpts); // {} default

        for (int i = 0; i < request.Items.Count; i++)
        {
            var raw = request.Items[i];
            try
            {
                // ---- normalize + validate item ----
                var industry = (raw.Industry ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(industry)) throw new InvalidOperationException("industry is required.");

                var key = (raw.Key ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("key is required.");

                var category = NormalizeCategory(raw.Category);

                if (raw.Variants == null || raw.Variants.Count == 0)
                    throw new InvalidOperationException("at least one variant is required.");

                // ---- upsert item by (Industry, Key) ----
                var item = await _db.TemplateLibraryItems
                    .FirstOrDefaultAsync(x => x.Industry == industry && x.Key == key, ct);

                if (item == null)
                {
                    item = new TemplateLibraryItem
                    {
                        Id = Guid.NewGuid(),
                        Industry = industry,
                        Key = key,
                        Category = category,
                        IsFeatured = raw.IsFeatured
                    };
                    _db.TemplateLibraryItems.Add(item);
                    res.CreatedItems++;
                    await _db.SaveChangesAsync(ct); // ensure item.Id for variants
                }
                else
                {
                    // update mutable fields
                    item.Category = category;
                    item.IsFeatured = raw.IsFeatured;
                    res.UpdatedItems++;
                    await _db.SaveChangesAsync(ct);
                }

                // ---- per-variant upsert by (LibraryItemId, Language) ----
                for (int v = 0; v < raw.Variants.Count; v++)
                {
                    var vv = raw.Variants[v];

                    var lang = (vv.Language ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(lang))
                        throw new InvalidOperationException("variant.language is required.");

                    var headerType = NormalizeHeaderType(vv.HeaderType);
                    var bodyText = (vv.BodyText ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(bodyText))
                        throw new InvalidOperationException("variant.bodyText is required.");

                    if (headerType == "TEXT" && string.IsNullOrWhiteSpace(vv.HeaderText))
                        throw new InvalidOperationException("headerText is required when headerType=TEXT.");

                    var buttonsJson = ButtonsToJson(vv.Buttons ?? new());
                    var examplesJson = ExamplesToJson(vv.Examples);

                    var existing = await _db.TemplateLibraryVariants
                        .FirstOrDefaultAsync(x => x.LibraryItemId == item.Id && x.Language == lang, ct);

                    if (existing == null)
                    {
                        _db.TemplateLibraryVariants.Add(new TemplateLibraryVariant
                        {
                            Id = Guid.NewGuid(),
                            LibraryItemId = item.Id,
                            Language = lang,
                            HeaderType = headerType,
                            HeaderText = vv.HeaderText,
                            BodyText = bodyText,
                            FooterText = vv.FooterText,
                            ButtonsJson = buttonsJson,
                            ExampleParamsJson = examplesJson
                        });
                        res.CreatedVariants++;
                    }
                    else
                    {
                        existing.HeaderType = headerType;
                        existing.HeaderText = vv.HeaderText;
                        existing.BodyText = bodyText;
                        existing.FooterText = vv.FooterText;
                        existing.ButtonsJson = buttonsJson;
                        existing.ExampleParamsJson = examplesJson;
                        res.UpdatedVariants++;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Errors.Add(new LibraryImportError
                {
                    ItemIndex = i,
                    Message = ex.Message
                });
            }
        }

        //await _db.SaveChangesAsync(ct);
        //await tx.CommitAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (dryRun)
        {
            // Undo all writes made within this transaction
            await tx.RollbackAsync(ct);
        }
        else
        {
            await tx.CommitAsync(ct);
        }
        res.TotalItems = request.Items.Count;
        res.Success = res.Errors.Count == 0;
        return res;
    }

    public async Task<TemplateLibraryListResponse> SearchAsync(
    string? industry, string? q, string? sort, int page, int pageSize, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _db.TemplateLibraryItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(industry))
        {
            var ind = industry.Trim().ToUpperInvariant();
            query = query.Where(x => x.Industry == ind);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var tq = q.Trim();
            query = query.Where(x =>
                x.Key.Contains(tq) ||
                x.Category.Contains(tq) ||
                x.Industry.Contains(tq));
        }

        // Sorting
        sort = (sort ?? "featured").ToLowerInvariant();
        query = sort switch
        {
            "name" => query.OrderBy(x => x.Key).ThenBy(x => x.Industry),
            "featured" => query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.Key),
            _ => query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.Key)
        };

        var total = await query.CountAsync(ct);

        var pageItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Industry,
                x.Key,
                x.Category,
                x.IsFeatured,
                // representative variant: prefer en_US if present; otherwise first by language asc
                Variant = _db.TemplateLibraryVariants
                            .AsNoTracking()
                            .Where(v => v.LibraryItemId == x.Id)
                            .OrderByDescending(v => v.Language == "en_US")
                            .ThenBy(v => v.Language)
                            .Select(v => new { v.Language, v.HeaderType, v.BodyText, v.ButtonsJson })
                            .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = new List<TemplateLibraryListItemDto>(pageItems.Count);

        foreach (var row in pageItems)
        {
            var lang = row.Variant?.Language ?? "en_US";
            var headerType = row.Variant?.HeaderType ?? "NONE";
            var body = row.Variant?.BodyText ?? "";

            // Placeholder count in body {{n}}
            var ph = Regex.Matches(body, @"\{\{\d+\}\}").Count;

            // Body preview (first 120 chars, one-line)
            var preview = body.Replace("\r", " ").Replace("\n", " ");
            if (preview.Length > 120) preview = preview.Substring(0, 120) + "…";

            // Buttons summary from JSON (keep simple & resilient)
            string btnSummary = "";
            try
            {
                var rawJson = row.Variant?.ButtonsJson ?? "[]";
                var btns = System.Text.Json.JsonSerializer.Deserialize<List<ButtonDto>>(rawJson)
                           ?? new List<ButtonDto>();

                if (btns.Count > 0)
                {
                    btnSummary = string.Join(", ",
                        btns.Select(b => (b.Type ?? "").ToUpperInvariant())
                            .Distinct());
                }
            }
            catch { /* ignore malformed buttons json */ }

            items.Add(new TemplateLibraryListItemDto
            {
                Id = row.Id,
                Industry = row.Industry,
                Key = row.Key,
                Category = row.Category,
                IsFeatured = row.IsFeatured,
                Language = lang,
                HeaderType = headerType,
                Placeholders = ph,
                BodyPreview = preview,
                ButtonsSummary = btnSummary
            });
        }

        return new TemplateLibraryListResponse
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }



    public async Task<LibraryImportRequest> ExportAsync(string? industry, CancellationToken ct = default)
    {
        var request = new LibraryImportRequest();

        var q = _db.TemplateLibraryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(industry))
        {
            var ind = industry.Trim().ToUpperInvariant();
            q = q.Where(x => x.Industry == ind);
        }

        var items = await q
            .OrderByDescending(x => x.IsFeatured)
            .ThenBy(x => x.Industry)
            .ThenBy(x => x.Key)
            .ToListAsync(ct);

        if (items.Count == 0)
            return request;

        var variantsLookup = await _db.TemplateLibraryVariants
            .AsNoTracking()
            .Where(v => items.Select(i => i.Id).Contains(v.LibraryItemId))
            .GroupBy(v => v.LibraryItemId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);

        foreach (var it in items)
        {
            variantsLookup.TryGetValue(it.Id, out var vs);
            var dto = new LibraryImportItem
            {
                Industry = it.Industry,
                Key = it.Key,
                Category = it.Category,
                IsFeatured = it.IsFeatured,
                Variants = new List<LibraryImportVariant>()
            };

            if (vs != null)
            {
                foreach (var v in vs.OrderBy(v => v.Language))
                {
                    List<LibraryImportButton> buttons;
                    try
                    {
                        buttons = string.IsNullOrWhiteSpace(v.ButtonsJson)
                            ? new List<LibraryImportButton>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<LibraryImportButton>>(v.ButtonsJson)
                              ?? new List<LibraryImportButton>();
                    }
                    catch
                    {
                        buttons = new List<LibraryImportButton>();
                    }

                    Dictionary<string, string>? examples;
                    try
                    {
                        examples = string.IsNullOrWhiteSpace(v.ExampleParamsJson)
                            ? new Dictionary<string, string>()
                            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v.ExampleParamsJson)
                              ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        examples = new Dictionary<string, string>();
                    }

                    dto.Variants.Add(new LibraryImportVariant
                    {
                        Language = v.Language,
                        HeaderType = v.HeaderType,
                        HeaderText = v.HeaderText,
                        BodyText = v.BodyText ?? string.Empty,
                        FooterText = v.FooterText,
                        Buttons = buttons,
                        Examples = examples
                    });
                }
            }

            request.Items.Add(dto);
        }

        return request;
    }

}