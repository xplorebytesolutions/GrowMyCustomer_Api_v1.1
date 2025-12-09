using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.WhatsAppSettings.Controllers
{
    [ApiController]
    [Route("api/templates/meta")]
    [Authorize]
    public sealed class TemplateMetaController : ControllerBase
    {
        private readonly IWhatsAppTemplateFetcherService _fetcher;

        public TemplateMetaController(IWhatsAppTemplateFetcherService fetcher)
        {
            _fetcher = fetcher;
        }

        
        [HttpGet("list/{businessId:guid}")]
        public async Task<IActionResult> List(Guid businessId, [FromQuery] string? provider = null)
        {
            if (businessId == Guid.Empty) return BadRequest(new { message = "Invalid businessId" });
            var list = await _fetcher.GetTemplatesMetaAsync(businessId, provider);
            return Ok(list);
        }

        // GET /api/templates/meta/{businessId}/{templateName}?language=en_US&provider=META_CLOUD
        [HttpGet("{businessId:guid}/{templateName}")]
        public async Task<IActionResult> One(Guid businessId, string templateName, [FromQuery] string? language = null, [FromQuery] string? provider = null)
        {
            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(templateName))
                return BadRequest(new { message = "Invalid parameters" });

            var meta = await _fetcher.GetTemplateMetaAsync(businessId, templateName, language, provider);
            if (meta is null) return NotFound();
            return Ok(meta);
        }
    }
}
