using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat_api.WhatsAppSettings.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WhatsAppTemplateFetcherController : ControllerBase
    {
        private readonly IWhatsAppTemplateFetcherService _templateFetcherService;

        public WhatsAppTemplateFetcherController(IWhatsAppTemplateFetcherService templateFetcherService)
        {
            _templateFetcherService = templateFetcherService;
        }

        [HttpGet("get-template/{businessId}")]
        [Authorize] // ✅ Optional: Require authentication if your project uses JWT auth
        public async Task<IActionResult> FetchTemplates(Guid businessId)
        {
            if (businessId == Guid.Empty)
                return BadRequest(new { message = "❌ Invalid BusinessId." });

            var templates = await _templateFetcherService.FetchTemplatesAsync(businessId); // comment this line to stop fetch template as per businessid
            //var templates = await _templateFetcherService.FetchAllTemplatesAsync(); // comment this line to stop fetch template as per businessid

            return Ok(new
            {
                success = true,
                templates = templates
            });
        }

      

        [HttpGet("get-template-all")]
        public async Task<IActionResult> GetAllTemplatesAsync()
        {
            try
            {
                var templates = await _templateFetcherService.FetchAllTemplatesAsync();
                return Ok(new { success = true, templates });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching templates",
                    detail = ex.Message
                });
            }
        }

        [HttpGet("get-template-by-name")]
        public async Task<IActionResult> GetTemplateByName([FromQuery] string name)
        {
            var businessId = Guid.Parse(User.FindFirst("businessId")?.Value);
            var template = await _templateFetcherService.GetTemplateByNameAsync(businessId, name, true);
            return template == null ? NotFound() : Ok(template);
        }
        [HttpGet("get-by-name/{businessId}/{templateName}")]
        public async Task<IActionResult> GetByName(Guid businessId, string templateName, [FromQuery] bool includeButtons = true)
        {
            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(templateName))
                return BadRequest(new { success = false, message = "❌ Missing or invalid parameters." });

            var template = await _templateFetcherService.GetTemplateByNameAsync(businessId, templateName, includeButtons);

            if (template == null)
                return NotFound();

            return Ok(new
            {
                success = true,
                template
            });
        }
    }
}

