using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.AutoReplyBuilder.Controllers
{
    [ApiController]
    [Route("api/auto-reply-runtime")]
    [Authorize]
    public class AutoReplyRuntimeController : ControllerBase
    {
        private readonly IAutoReplyRuntimeService _runtimeService;
        private readonly ILogger<AutoReplyRuntimeController> _logger;

        public AutoReplyRuntimeController(
            IAutoReplyRuntimeService runtimeService,
            ILogger<AutoReplyRuntimeController> logger)
        {
            _runtimeService = runtimeService;
            _logger = logger;
        }

        // 🔁 Simple button-click matcher (uses the new keyword matcher under the hood)
        [HttpPost("button-click")]
        public async Task<ActionResult<AutoReplyMatchResultDto>> HandleButtonClick([FromBody] AutoReplyButtonClickDto dto)
        {
            var businessId = ClaimsBusinessDetails.GetBusinessId(User);

            _logger.LogInformation("🔘 Button clicked: BusinessId={BusinessId}, Phone={Phone}, Button={ButtonText}, RefMsg={RefMessageId}",
                businessId, dto.Phone, dto.ButtonText, dto.RefMessageId?.ToString() ?? "null");

            var result = await _runtimeService.FindMatchAsync(
                new AutoReplyMatchRequestDto
                {
                    BusinessId = businessId,
                    IncomingText = dto.ButtonText ?? string.Empty
                });

            return Ok(result);
        }

        // 🧪 Manual test (canvas-based flow trigger) - now returns a match preview only
        [HttpPost("flow-by-button")]
        public async Task<ActionResult<AutoReplyMatchResultDto>> TriggerFlowByButton([FromBody] AutoReplyButtonClickDto dto)
        {
            var businessId = dto.BusinessId != Guid.Empty ? dto.BusinessId : ClaimsBusinessDetails.GetBusinessId(User);
            var match = await _runtimeService.FindMatchAsync(new AutoReplyMatchRequestDto
            {
                BusinessId = businessId,
                IncomingText = dto.ButtonText ?? string.Empty
            });

            return Ok(match);
        }
    }
}
