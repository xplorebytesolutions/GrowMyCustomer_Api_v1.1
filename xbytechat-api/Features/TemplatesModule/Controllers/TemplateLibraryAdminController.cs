using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;

namespace xbytechat.api.Features.TemplateModule.Controllers
{
    [ApiController]
    [Route("api/admin/template-builder/library")] // <-- unique admin prefix (no conflict with public controller)
    [Authorize]                                    // add policy/roles as needed
    [Produces("application/json")]
    public sealed class TemplateLibraryAdminController : ControllerBase
    {
        private readonly ITemplateLibraryService _library;

        public TemplateLibraryAdminController(ITemplateLibraryService library)
        {
            _library = library;
        }

        // POST /api/admin/template-builder/library/import?dryRun=true
        [HttpPost("import")]
        public async Task<ActionResult<object>> Import(
            [FromBody] LibraryImportRequest request,
            [FromQuery] bool dryRun = false,
            CancellationToken ct = default)
        {
            if (request is null || request.Items is null)
                return BadRequest(new { success = false, message = "Invalid request payload." });

            // If you want admin-only: [Authorize(Policy = "templates.library.import")] at class or action level.

            var result = await _library.ImportAsync(request, dryRun, ct);

            return Ok(new
            {
                success = result.Success,
                dryRun,
                result.TotalItems,
                result.CreatedItems,
                result.UpdatedItems,
                result.CreatedVariants,
                result.UpdatedVariants,
                errors = result.Errors
            });
        }

        // GET /api/admin/template-builder/library/browse?industry=...&q=...&sort=name&page=1&pageSize=20
        [HttpGet("browse")]
        public async Task<ActionResult<object>> Browse(
            [FromQuery] string? industry,
            [FromQuery] string? q,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var resp = await _library.SearchAsync(industry, q, sort, page, pageSize, ct);
            return Ok(resp);
        }

        // GET /api/admin/template-builder/library/export?industry=SALON
        [HttpGet("export")]
        public async Task<ActionResult<object>> Export([FromQuery] string? industry, CancellationToken ct = default)
        {
            var payload = await _library.ExportAsync(industry, ct);
            return Ok(new
            {
                success = true,
                count = payload.Items.Count,
                items = payload.Items
            });
        }
    }
}
