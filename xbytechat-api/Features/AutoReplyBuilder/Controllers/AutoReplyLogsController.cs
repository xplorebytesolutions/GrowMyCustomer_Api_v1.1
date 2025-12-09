using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.AutoReplyBuilder.Controllers
{
    /// <summary>
    /// Read-only endpoints for AutoReply logs, used by the builder UI.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // same auth pattern as other feature controllers
    public class AutoReplyLogsController : ControllerBase
    {
        private readonly IAutoReplyLogService _logService;

        public AutoReplyLogsController(IAutoReplyLogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// Returns most recent auto-reply triggers for the current business.
        /// GET /api/autoreplylogs/recent?take=20
        /// </summary>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(IReadOnlyList<AutoReplyLogSummaryDto>), 200)]
        public async Task<ActionResult<IReadOnlyList<AutoReplyLogSummaryDto>>> GetRecentAsync(
            [FromQuery] int take = 20,
            CancellationToken cancellationToken = default)
        {
            // BusinessId comes from JWT claims
            var businessId = User.GetBusinessId();

            if (businessId == Guid.Empty)
            {
                return Unauthorized("Missing or invalid business id in token.");
            }

            var items = await _logService.GetRecentAsync(
                businessId,
                take,
                cancellationToken);

            return Ok(items);
        }
    }
}
