using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.TemplateModule.Controllers;

[ApiController]
[Route("api/template-builder/uploads")]
[Authorize]
[Produces("application/json")]
public sealed class TemplateUploadsController : ControllerBase
{
    private readonly IMetaUploadService _uploader;
    private readonly ITemplateDraftService _drafts;

    public TemplateUploadsController(IMetaUploadService uploader, ITemplateDraftService drafts)
    {
        _uploader = uploader;
        _drafts = drafts;
    }

    // ---------- Form DTO so Swagger can model multipart correctly ----------
    public sealed class UploadHeaderForm
    {
        /// <summary>Draft Id to update</summary>
        [Required]
        public Guid DraftId { get; set; }

        /// <summary>Variant language, e.g. en_US</summary>
        [Required]
        public string Language { get; set; } = default!;

        /// <summary>IMAGE | VIDEO | DOCUMENT</summary>
        [Required]
        public string MediaType { get; set; } = default!;

        /// <summary>Upload a file (use either File or SourceUrl)</summary>
        public IFormFile? File { get; set; }

        /// <summary>Remote URL to fetch and upload (use either File or SourceUrl)</summary>
        public string? SourceUrl { get; set; }

        /// <summary>Optional override filename (improves MIME guess)</summary>
        public string? FileName { get; set; }
    }

    // POST /api/template-builder/uploads/header
    [HttpPost("header")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(64_000_000)] // 64MB cap; per-type limits enforced in service
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> UploadHeader([FromForm] UploadHeaderForm form, CancellationToken ct)
    {
        var businessId = User.GetBusinessId();
        if (businessId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

        if (form is null)
            return BadRequest(new { success = false, message = "Empty form." });

        if (string.IsNullOrWhiteSpace(form.Language))
            return BadRequest(new { success = false, message = "language is required." });

        if ((form.File is null && string.IsNullOrWhiteSpace(form.SourceUrl)) ||
            (form.File is not null && !string.IsNullOrWhiteSpace(form.SourceUrl)))
        {
            return BadRequest(new { success = false, message = "Provide either a file or a sourceUrl (not both)." });
        }

        if (!Enum.TryParse<HeaderMediaType>(form.MediaType?.Trim(), true, out var kind))
            return BadRequest(new { success = false, message = "mediaType must be IMAGE, VIDEO, or DOCUMENT." });

        Stream? stream = null;
        string? fileName = form.FileName;

        if (form.File is not null)
        {
            if (form.File.Length <= 0)
                return BadRequest(new { success = false, message = "Empty file." });

            stream = form.File.OpenReadStream();
            fileName ??= form.File.FileName;
        }

        try
        {
            var result = await _uploader.UploadHeaderAsync(
                businessId: businessId,
                mediaType: kind,
                fileStream: stream,
                fileName: fileName,
                sourceUrl: form.SourceUrl,
                ct: ct
            );

            // Persist handle on the variant. Service stores "handle:{handle}" internally.
            var ok = await _drafts.SetHeaderHandleAsync(
                businessId,
                form.DraftId,
                form.Language,
                kind.ToString(),
                result.Handle,
                ct
            );

            if (!ok)
                return NotFound(new { success = false, message = "Draft not found for this business." });

            return Ok(new
            {
                success = true,
                message = result.IsStub ? "Uploaded (stub handle generated)." : "Uploaded successfully.",
                draftId = form.DraftId,
                language = form.Language,
                mediaType = kind.ToString(),
                handle = result.Handle,   // e.g., "4::abcdef..."
                mime = result.MimeType,
                bytes = result.SizeBytes,
                isStub = result.IsStub
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using xbytechat.api.Features.TemplateModule.Abstractions;
//using xbytechat.api.Shared;

//namespace xbytechat.api.Features.TemplateModule.Controllers;

//[ApiController]
//[Route("api/template-builder/uploads")]
//[Authorize]
//public sealed class TemplateUploadsController : ControllerBase
//{
//    private readonly IMetaUploadService _uploader;
//    private readonly ITemplateDraftService _drafts;

//    public TemplateUploadsController(IMetaUploadService uploader, ITemplateDraftService drafts)
//    {
//        _uploader = uploader;
//        _drafts = drafts;
//    }

//    // POST /api/template-builder/uploads/header
//    // multipart/form-data:
//    //  - draftId: guid (required)
//    //  - language: string (required)
//    //  - mediaType: IMAGE|VIDEO|DOCUMENT (required)
//    //  - file: IFormFile (optional if sourceUrl provided)
//    //  - sourceUrl: string (optional if file provided)
//    [HttpPost("header")]
//    [RequestSizeLimit(64_000_000)] // 64MB cap; actual limits enforced per type in service
//    public async Task<ActionResult<object>> UploadHeader(
//        [FromForm] Guid draftId,
//        [FromForm] string language,
//        [FromForm] string mediaType,
//        [FromForm] IFormFile? file,
//        [FromForm] string? sourceUrl,
//        CancellationToken ct)
//    {
//        var businessId = User.GetBusinessId();
//        if (businessId == Guid.Empty)
//            return Unauthorized(new { success = false, message = "Invalid or missing BusinessId claim." });

//        if (draftId == Guid.Empty)
//            return BadRequest(new { success = false, message = "Invalid draftId." });

//        if (string.IsNullOrWhiteSpace(language))
//            return BadRequest(new { success = false, message = "language is required." });

//        if (!Enum.TryParse<HeaderMediaType>(mediaType, true, out var kind))
//            return BadRequest(new { success = false, message = "mediaType must be IMAGE, VIDEO, or DOCUMENT." });

//        Stream? stream = null;
//        string? fileName = null;

//        if (file is not null)
//        {
//            if (file.Length <= 0) return BadRequest(new { success = false, message = "Empty file." });
//            stream = file.OpenReadStream();
//            fileName = file.FileName;
//        }

//        try
//        {
//            var result = await _uploader.UploadHeaderAsync(businessId, kind, stream, fileName, sourceUrl, ct);

//            // Persist handle on the variant
//            var ok = await _drafts.SetHeaderHandleAsync(
//                businessId, draftId, language, kind.ToString(), result.Handle, ct);

//            if (!ok) return NotFound(new { success = false, message = "Draft not found for this business." });

//            return Ok(new
//            {
//                success = true,
//                message = result.IsStub ? "Uploaded (stub handle generated)." : "Uploaded successfully.",
//                draftId,
//                language,
//                mediaType = kind.ToString(),
//                handle = result.Handle, // raw handle; draft stores "handle:{handle}"
//                mime = result.MimeType,
//                size = result.SizeBytes,
//                isStub = result.IsStub
//            });
//        }
//        catch (Exception ex)
//        {
//            return BadRequest(new { success = false, message = ex.Message });
//        }
//    }
//}
