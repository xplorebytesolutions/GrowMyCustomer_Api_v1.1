using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Automation.DTOs;
using xbytechat.api.Features.Automation.Repositories;
using xbytechat.api.Features.Automation.Services;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;
using xbytechat.api.Features.CRM.Interfaces;

namespace xbytechat.api.Features.Automation.Controllers
{
    [ApiController]
    [Route("api/automation")]
    [Authorize]
    public class AutomationController : ControllerBase
    {
        private readonly IAutomationFlowRepository _automationRepository;
        private readonly IAutomationRunner _automationRunner;
        private readonly IContactService _contactService;

        public AutomationController(
            IAutomationFlowRepository automationRepository,
            IAutomationRunner automationRunner,
            IContactService contactService)
        {
            _automationRepository = automationRepository;
            _automationRunner = automationRunner;
            _contactService = contactService;
        }

        [HttpPost("trigger")]
        public async Task<IActionResult> TriggerByKeyword([FromBody] AutomationTriggerRequest request)
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (string.IsNullOrWhiteSpace(request.Keyword) || string.IsNullOrWhiteSpace(request.Phone))
                return BadRequest("Keyword and phone are required.");

            var flow = await _automationRepository.GetFlowByKeywordAsync(businessId, request.Keyword);
            if (flow == null || !flow.IsActive)
                return NotFound("⚠️ No matching active automation flow found.");

            var contact = await _contactService.FindOrCreateAsync(businessId, request.Phone);

            var result = await _automationRunner.RunFlowAsync(
                flow,
                businessId,
                contact.Id,
                request.Phone,
                request.SourceChannel ?? "manual",
                request.IndustryTag ?? "manual"
            );

            return Ok(result);
        }

    }
}
