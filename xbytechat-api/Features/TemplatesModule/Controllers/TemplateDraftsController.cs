using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Features.TemplateModule.Validators;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Shared;
using xbytechat.api.Features.TemplateModule.Services; // User.GetBusinessId()

namespace xbytechat.api.Features.TemplateModule.Controllers;

[ApiController]
[Route("api/template-builder")]
[Authorize]
public sealed class TemplateDraftsController : ControllerBase
{
    private readonly ITemplateDraftService _drafts;
    private readonly ITemplateSubmissionService _submitter;
    private readonly ITemplateNameCheckService _nameCheck;
    private readonly ITemplateStatusService _status;
    private readonly ITemplateDraftLifecycleService _lifecycle;

    public TemplateDraftsController(
        ITemplateDraftService drafts,
        ITemplateSubmissionService submitter,
        ITemplateNameCheckService nameCheck,
        ITemplateStatusService status,
        ITemplateDraftLifecycleService lifecycle)
    {
        _drafts = drafts;
        _submitter = submitter;
        _status = status;
        _nameCheck = nameCheck;
        _lifecycle = lifecycle;
    }

    // POST /api/template-builder/drafts
    [HttpPost("drafts")]
    public async Task<ActionResult<object>> CreateDraft([FromBody] TemplateDraftCreateDto dto, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (dto is null)
            return BadRequest(new { success = false, message = "Body required" });

        if (string.IsNullOrWhiteSpace(dto.Key))
            return BadRequest(new { success = false, message = "Key is required." });

        try
        {
            var draft = await _drafts.CreateDraftAsync(businessId, dto, ct);
            return CreatedAtAction(nameof(GetDraft), new { draftId = draft.Id }, new
            {
                success = true,
                message = "Draft created.",
                draftId = draft.Id,
                key = draft.Key,
                category = draft.Category,
                defaultLanguage = draft.DefaultLanguage
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
    }

    // GET /api/template-builder/drafts
    [HttpGet("drafts")]
    public async Task<ActionResult<object>> ListDrafts(CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        var items = await _drafts.ListDraftsAsync(businessId, ct);
        return Ok(new
        {
            success = true,
            message = "Drafts loaded.",
            businessId,
            count = items.Count,
            items = items.Select(d => new { d.Id, d.Key, d.Category, d.DefaultLanguage, d.UpdatedAt })
        });
    }

    // GET /api/template-builder/drafts/{draftId}
    [HttpGet("drafts/{draftId:guid}")]
    public async Task<ActionResult<object>> GetDraft(Guid draftId, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        var draft = await _drafts.GetDraftAsync(businessId, draftId, ct);
        if (draft is null)
            return NotFound(new { success = false, message = "Draft not found." });

        return Ok(new
        {
            success = true,
            message = "Draft loaded.",
            draft
        });
    }

    // POST /api/template-builder/drafts/{draftId}/variants
    [HttpPost("drafts/{draftId:guid}/variants")]
    public async Task<ActionResult<object>> UpsertVariant(Guid draftId, [FromBody] TemplateDraftVariantUpsertDto dto, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        if (dto is null)
            return BadRequest(new { success = false, message = "Body required" });

        if (string.IsNullOrWhiteSpace(dto.Language)) dto.Language = "en_US";
        if (string.IsNullOrWhiteSpace(dto.HeaderType)) dto.HeaderType = "NONE";

        try
        {
            var variant = await _drafts.UpsertVariantAsync(businessId, draftId, dto, ct);
            return Accepted(new
            {
                success = true,
                message = "Variant saved.",
                draftId,
                language = variant.Language
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Draft not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // POST /api/template-builder/drafts/{draftId}/validate
    // Validation endpoint stays payload-only (single variant)
    [HttpPost("drafts/{draftId:guid}/validate")]
    public ActionResult<object> Validate(Guid draftId, [FromBody] TemplateDraftVariantUpsertDto body)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        if (body is null)
            return BadRequest(new { success = false, message = "Body required." });

        var result = VariantValidator.Validate(body);
        return Ok(new
        {
            success = result.Ok,
            message = result.Ok ? "Validation passed." : "Validation failed.",
            businessId,
            draftId,
            errors = result.Errors
        });
    }

    // POST /api/template-builder/drafts/{draftId}/validate-all
    [HttpPost("drafts/{draftId:guid}/validate-all")]
    public async Task<ActionResult<object>> ValidateAll(Guid draftId, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        var (ok, errors) = await _drafts.ValidateAllAsync(businessId, draftId, ct);

        return Ok(new
        {
            success = ok,
            message = ok
                ? "All language variants are valid and consistent."
                : "Validation failed for one or more languages.",
            draftId,
            errors
        });
    }

    // POST /api/template-builder/drafts/{draftId}/submit
    [HttpPost("drafts/{draftId:guid}/submit")]
    public async Task<ActionResult<object>> Submit(Guid draftId, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        var resp = await _submitter.SubmitAsync(businessId, draftId, ct);
        if (!resp.Success)
            return BadRequest(resp);

        return Accepted(resp);
    }

    // GET /api/template-builder/drafts/{draftId}/status
    [HttpGet("drafts/{draftId:guid}/status")]
    public async Task<ActionResult<object>> GetStatus(Guid draftId, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        try
        {
            var (metaName, items) = await _status.GetStatusAsync(businessId, draftId, ct);
            return Ok(new
            {
                success = true,
                message = items.Count == 0
                    ? "No synced rows yet. Try again later."
                    : "Latest status from WhatsAppTemplates.",
                draftId,
                name = metaName,
                items
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Draft not found." });
        }
    }

    // ✅ FIXED ROUTE:
    // GET /api/template-builder/drafts/{draftId}/preview?language=en_US
    [HttpGet("drafts/{draftId:guid}/preview")]
    public async Task<ActionResult<object>> Preview(Guid draftId, [FromQuery] string language, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (draftId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid draftId." });

        if (string.IsNullOrWhiteSpace(language))
            return BadRequest(new { success = false, message = "language is required." });

        var dto = await _drafts.GetPreviewAsync(businessId, draftId, language, ct);
        if (dto is null)
            return NotFound(new { success = false, message = "Draft or language variant not found." });

        return Ok(new { success = true, preview = dto });
    }

    // GET /api/template-builder/drafts/{draftId}/name-check?language=en_US
    [HttpGet("drafts/{draftId:guid}/name-check")]
    public async Task<ActionResult<object>> NameCheck(Guid draftId, [FromQuery] string? language, CancellationToken ct = default)
    {
        var businessId = User.GetBusinessId();
        var lang = string.IsNullOrWhiteSpace(language) ? "en_US" : language;

        var result = await _nameCheck.CheckAsync(businessId, draftId, lang, ct);
        if (result is null)
            return NotFound(new { success = false, message = "Draft not found." });

        return Ok(new
        {
            success = true,
            name = result.Name,
            available = result.Available,
            suggestion = result.Suggestion,
            language = result.Language,
            draftId = result.DraftId
        });
    }

    // POST /api/template-builder/drafts/{draftId}/duplicate
    [HttpPost("drafts/{draftId:guid}/duplicate")]
    public async Task<ActionResult<object>> Duplicate(Guid draftId, CancellationToken ct = default)
    {
        var businessId = User.GetBusinessId();
        var dup = await _lifecycle.DuplicateDraftAsync(businessId, draftId, ct);
        return Ok(new { success = true, draftId = dup.Id, key = dup.Key });
    }

    // DELETE /api/template-builder/drafts/{draftId}
    [HttpDelete("drafts/{draftId:guid}")]
    public async Task<ActionResult<object>> Delete(Guid draftId, CancellationToken ct = default)
    {
        var businessId = User.GetBusinessId();
        var ok = await _lifecycle.DeleteDraftAsync(businessId, draftId, ct);
        if (!ok) return NotFound(new { success = false, message = "Draft not found." });
        return Ok(new { success = true });
    }

    // DELETE /api/template-builder/templates/{name}?language=en_US
    [HttpDelete("~/api/template-builder/templates/{name}")]
    public async Task<ActionResult<object>> DeleteApprovedTemplate(
        [FromRoute] string name,
        [FromQuery] string language = "en_US",
        CancellationToken ct = default)
    {
        var businessId = User.GetBusinessId();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Template name is required." });

        var ok = await _lifecycle.DeleteApprovedTemplateAsync(businessId, name, language, ct);
        return Ok(new { success = ok, name, language });
    }
}
