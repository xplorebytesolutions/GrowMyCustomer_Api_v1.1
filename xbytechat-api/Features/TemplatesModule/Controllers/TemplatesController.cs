using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TemplateModule.Services;

namespace xbytechat.api.Features.TemplateModule.Controllers
{
    [ApiController]
    [Route("api/templates")]
    public class TemplatesController : ControllerBase
    {
        private readonly IWhatsAppTemplateService _templateService;
        private readonly ILogger<TemplatesController> _logger;

        public TemplatesController(IWhatsAppTemplateService templateService, ILogger<TemplatesController> logger)
        {
            _templateService = templateService;
            _logger = logger;
        }

        /// <summary>
        /// Fetches WhatsApp template metadata (name, language, body, placeholders)
        /// </summary>
        [HttpGet("metadata")]
        public async Task<IActionResult> GetTemplates()
        {
            try
            {
                var templates = await _templateService.FetchTemplatesAsync();
                return Ok(new
                {
                    success = true,
                    templates
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching template metadata: " + ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    message = "❌ Failed to retrieve template metadata",
                    error = ex.Message
                });
            }
        }
    }
}
