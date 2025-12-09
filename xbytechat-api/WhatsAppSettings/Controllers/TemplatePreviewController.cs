using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.WhatsAppSettings.Controllers
{
    [ApiController]
    [Route("api/templates/preview")]
    [Authorize]
    public sealed class TemplatePreviewController : ControllerBase
    {
        private readonly ITemplatePreviewService _svc;

        public TemplatePreviewController(ITemplatePreviewService svc)
        {
            _svc = svc;
        }

        // POST /api/templates/preview/{businessId}
        [HttpPost("{businessId:guid}")]
        public async Task<IActionResult> TemplatePreview([FromRoute] Guid businessId, [FromBody] TemplatePreviewRequestDto request)
        {
            if (businessId == Guid.Empty) return BadRequest(new { message = "Invalid businessId" });
            if (request == null || string.IsNullOrWhiteSpace(request.TemplateName))
                return BadRequest(new { message = "TemplateName is required." });

            var result = await _svc.PreviewAsync(businessId, request);
            return Ok(result);
        }
    }
}
