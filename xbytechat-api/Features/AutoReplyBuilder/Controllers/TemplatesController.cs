using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.TemplateMessages.DTOs;

namespace xbytechat.api.Features.TemplateMessages.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TemplatesController : ControllerBase
    {
        private static readonly List<TemplateDto> MockTemplates = new()
        {
            new TemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Welcome Template",
                Placeholders = 2
            },
            new TemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Offer Reminder",
                Placeholders = 1
            },
            new TemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Follow Up",
                Placeholders = 3
            }
        };

        [HttpGet]
        public ActionResult<List<TemplateDto>> GetAll()
        {
            return Ok(MockTemplates);
        }
    }
}
