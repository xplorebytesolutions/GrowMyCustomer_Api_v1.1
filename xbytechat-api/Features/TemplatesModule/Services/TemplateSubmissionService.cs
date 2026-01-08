using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Payload;
using xbytechat.api.Features.TemplateModule.Utils;
using xbytechat.api.Features.TemplateModule.Validators;
using xbytechat.api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class TemplateSubmissionService : ITemplateSubmissionService
{
    private readonly AppDbContext _db;
    private readonly IMetaTemplateClient _meta;
    private readonly ITemplateSyncService _templateSyncService;
    public TemplateSubmissionService(AppDbContext db, IMetaTemplateClient meta, ITemplateSyncService templateSyncService)
    {
        _db = db;
        _meta = meta;
        _templateSyncService = templateSyncService;
        
    }

    public async Task<TemplateSubmitResponseDto> SubmitAsync(Guid businessId, Guid draftId, CancellationToken ct = default)
    {
        // 1) Load draft + variants
        var draft = await _db.TemplateDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == draftId && x.BusinessId == businessId, ct);
        if (draft is null)
            return new TemplateSubmitResponseDto { Success = false, Message = "Draft not found." };

        var variants = await _db.TemplateDraftVariants
            .AsNoTracking()
            .Where(v => v.TemplateDraftId == draft.Id)
            .ToListAsync(ct);
        if (variants.Count == 0)
            return new TemplateSubmitResponseDto { Success = false, Message = "No language variants to submit." };

        // 2) Validate cross-language parity
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
        if (!ok)
        {
            var errMsg = string.Join(" | ", errors.Select(kv => $"{kv.Key}: {string.Join(" ; ", kv.Value)}"));
            return new TemplateSubmitResponseDto
            {
                Success = false,
                Message = "Validation failed before submission.",
                Variants = variants.Select(v => new SubmittedVariantResult
                {
                    Language = v.Language,
                    Status = errors.ContainsKey(v.Language) ? "INVALID" : "READY",
                    RejectionReason = errors.ContainsKey(v.Language) ? string.Join(" ; ", errors[v.Language]) : null
                }).ToList()
            };
        }

        // 3) Use the user-entered draft Key as the Meta template name (no business suffixes / random chars).
        var name = (draft.Key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new TemplateSubmitResponseDto { Success = false, Message = "Template name is required." };

        // Meta template name rules: lowercase letters, numbers, underscores, must start with a letter, max 25 chars.
        if (name.Length > 25 || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9_]*$"))
        {
            return new TemplateSubmitResponseDto
            {
                Success = false,
                Message = "Invalid template name. Use lowercase letters/numbers/underscores, start with a letter, max 25 characters."
            };
        }

        // 4) Submit per language
        var results = new List<SubmittedVariantResult>();
        foreach (var v in variants)
        {
            var buttons = SafeDeserialize<List<ButtonDto>>(v.ButtonsJson) ?? new();
            var examples = SafeDeserialize<Dictionary<string, string>>(v.ExampleParamsJson) ?? new();

            // For media headers, HeaderMediaLocalUrl must be uploaded before CreateTemplate
            string? headerMetaId = null;
            if (!v.HeaderType.Equals("NONE", StringComparison.OrdinalIgnoreCase) &&
                !v.HeaderType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(v.HeaderMediaLocalUrl))
                {
                    results.Add(new SubmittedVariantResult
                    {
                        Language = v.Language,
                        Status = "FAILED",
                        RejectionReason = "Missing media for header."
                    });
                    continue;
                }
                try
                {
                    headerMetaId = await _meta.UploadMediaAsync(businessId, v.HeaderMediaLocalUrl!, v.HeaderType, ct);
                }
                catch (Exception ex)
                {
                    results.Add(new SubmittedVariantResult
                    {
                        Language = v.Language,
                        Status = "FAILED",
                        RejectionReason = ex.Message
                    });
                    continue;
                }
            }

            var (components, examplePayload) =
                MetaComponentsBuilder.Build(v.HeaderType, v.HeaderText, headerMetaId, v.BodyText, v.FooterText, buttons, examples);

            // DEBUG: Log the payload to catch invalid parameter sources
            Console.WriteLine($"[Meta Submit] Components: {System.Text.Json.JsonSerializer.Serialize(components)}");
            Console.WriteLine($"[Meta Submit] Examples: {System.Text.Json.JsonSerializer.Serialize(examplePayload)}");

            var created = await _meta.CreateTemplateAsync(
                businessId: businessId,
                name: name,
                category: draft.Category,
                language: v.Language,
                componentsPayload: components,
                examplesPayload: examplePayload,
                ct: ct
            );

            results.Add(new SubmittedVariantResult
            {
                Language = v.Language,
                Status = created.Success ? "PENDING" : "FAILED",
                RejectionReason = created.Success ? null : (created.Error ?? "Meta create call failed.")
            });
        }

        var success = results.All(r => r.Status == "PENDING");

        // 🔄 Auto-sync only if every language submitted OK
        if (success)
        {
            try
            {
                // Force = true (ignore TTL), onlyUpsert = true (don’t deactivate others)
                await _templateSyncService.SyncBusinessTemplatesAsync(businessId, force: true, onlyUpsert: true, ct);
            }
            catch (Exception ex)
            {
                // Non-fatal: submission succeeded; sync can be retried via existing endpoint
                Console.WriteLine($"[Template Submit] Sync warning: {ex.Message}");
            }
        }

        return new TemplateSubmitResponseDto
        {
            Success = success,
            Message = success ? "Submitted to Meta. Awaiting review." : "Submitted with some failures.",
            Variants = results
        };
    }

    public Task<int> SyncStatusAsync(Guid businessId, CancellationToken ct = default)
    {
        // Placeholder: in a later step we’ll call provider ListTemplates and update your WhatsAppTemplates via your existing sync flow.
        return Task.FromResult(0);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static T? SafeDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json!, JsonOpts); } catch { return default; }
    }
}
