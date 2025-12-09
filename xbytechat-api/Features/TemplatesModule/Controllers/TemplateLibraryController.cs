using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat_api.WhatsAppSettings.Services; // User.GetBusinessId()
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.TemplateModule.Controllers;

[ApiController]
[Route("api/template-builder/library")]
[Authorize]
[Produces("application/json")]
public sealed class TemplateLibraryController : ControllerBase
{
    private readonly ITemplateLibraryService _library;

    public TemplateLibraryController(ITemplateLibraryService library)
    {
        _library = library;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Legacy, simple list (kept under /list to avoid collision with /browse)
    // GET /api/template-builder/library/list?industry=SALON
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpGet("list")]
    public async Task<ActionResult<object>> List([FromQuery] string? industry, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        var items = await _library.ListAsync(industry, ct);

        return Ok(new
        {
            success = true,
            message = "Library loaded.",
            businessId,
            count = items.Count,
            items = items.Select(i => new { i.Id, i.Industry, i.Key, i.Category, i.IsFeatured })
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // New, paged & searchable browse endpoint used by the UI
    // GET /api/template-builder/library/browse?industry=&q=&sort=&page=&pageSize=
    // sort: "featured" | "name"
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpGet("browse")]
    public async Task<ActionResult<TemplateLibraryListResponse>> Browse(
        [FromQuery] string? industry,
        [FromQuery] string? q,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var data = await _library.SearchAsync(industry, q, sort, page, pageSize, ct);
        return Ok(data);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Activate (variant A) - legacy style
    // POST /api/template-builder/library/use/{libraryItemId}
    // body: { "languages": ["en_US","hi_IN"] }
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpPost("use/{libraryItemId:guid}")]
    public async Task<ActionResult<object>> Use(Guid libraryItemId, [FromBody] TemplateLibraryUseRequestDto body, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (libraryItemId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid libraryItemId." });

        if (body is null || body.Languages is null || body.Languages.Count == 0)
            return BadRequest(new { success = false, message = "At least one language is required." });

        var draft = await _library.InstantiateDraftAsync(businessId, libraryItemId, body.Languages, ct);

        // If your TemplateDraftsController exposes GetDraft, keep CreatedAtAction; otherwise return Ok(...)
        return CreatedAtAction("GetDraft", "TemplateDrafts", new { draftId = draft.Id }, new
        {
            success = true,
            message = "Draft created from library.",
            draftId = draft.Id,
            key = draft.Key,
            category = draft.Category
        });
    }

    public sealed class ActivateRequest
    {
        public IEnumerable<string>? Languages { get; set; } // e.g., ["en_US","hi_IN"]
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Activate (variant B) - current UI contract
    // POST /api/template-builder/library/{itemId}/activate
    // body: { "languages": ["en_US","hi_IN"] }  (supports "ALL" if your service does)
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpPost("{itemId:guid}/activate")]
    public async Task<ActionResult<object>> Activate(Guid itemId, [FromBody] ActivateRequest body, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (itemId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid itemId." });

        var langs = (body?.Languages ?? Array.Empty<string>()).ToArray();
        if (langs.Length == 0)
            return BadRequest(new { success = false, message = "At least one language is required. You can also pass \"ALL\"." });

        try
        {
            var draft = await _library.InstantiateDraftAsync(businessId, itemId, langs, ct);
            return Ok(new
            {
                success = true,
                message = "Template activated as draft.",
                draftId = draft.Id,
                draft
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Library item not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Get a library item with its variants for preview
    // GET /api/template-builder/library/item/{itemId}
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpGet("item/{itemId:guid}")]
    public async Task<ActionResult<object>> GetItem(Guid itemId, CancellationToken ct)
    {
        if (itemId == Guid.Empty)
            return BadRequest(new { success = false, message = "Invalid itemId." });

        try
        {
            var (item, variants) = await _library.GetItemAsync(itemId, ct);
            return Ok(new
            {
                success = true,
                item,
                variants
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Library item not found." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // List distinct industries available in the library
    // GET /api/template-builder/library/industries
    // ─────────────────────────────────────────────────────────────────────────────
    [HttpGet("industries")]
    public async Task<ActionResult<object>> Industries(CancellationToken ct)
    {
        var list = await _library.ListIndustriesAsync(ct);
        return Ok(new
        {
            success = true,
            count = list.Count,
            industries = list
        });
    }
}
