using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Auditing.FlowExecutions.DTOs;
using xbytechat.api.Features.Auditing.FlowExecutions.Services;
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.Auditing.FlowExecutions.Controllers
{
    /// <summary>
    /// Internal / debugging API for exploring flow execution logs.
    /// This is not meant to be exposed to end customers directly.
    /// </summary>
    [ApiController]
    [Route("api/flow-executions")]
    public class FlowExecutionsController : ControllerBase
    {
        private readonly IFlowExecutionQueryService _queryService;
        private readonly ILogger<FlowExecutionsController> _logger;

        public FlowExecutionsController(
            IFlowExecutionQueryService queryService,
            ILogger<FlowExecutionsController> logger)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns recent flow execution steps for a business, ordered by latest first.
        /// 
        /// Example:
        /// GET /api/flow-executions/recent?businessId=...&origin=AutoReply&limit=50
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<IReadOnlyList<FlowExecutionLogDto>>> GetRecent(
            [FromQuery] Guid businessId,
            [FromQuery] FlowExecutionOrigin? origin,
            [FromQuery] Guid? flowId,
            [FromQuery] string? contactPhone,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
            {
                return BadRequest("businessId is required.");
            }

            var filter = new FlowExecutionFilter
            {
                Origin = origin,
                FlowId = flowId,
                ContactPhone = contactPhone,
                Limit = limit
            };

            _logger.LogInformation(
                "[FlowExecutions] GetRecent biz={BusinessId} origin={Origin} flowId={FlowId} phone={Phone} limit={Limit}",
                businessId,
                origin,
                flowId,
                contactPhone,
                limit);

            var results = await _queryService
                .GetRecentExecutionsAsync(businessId, filter, ct)
                .ConfigureAwait(false);

            return Ok(results);
        }

        [HttpGet("run/{runId:guid}")]
        public async Task<ActionResult<IReadOnlyList<FlowExecutionLogDto>>> GetRunTimeline(
           [FromRoute] Guid runId,
           [FromQuery] Guid businessId,
           CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
            {
                return BadRequest("businessId is required");
            }

            if (runId == Guid.Empty)
            {
                return BadRequest("runId is required");
            }

            try
            {
                var rows = await _queryService.GetRunTimelineAsync(
                    businessId,
                    runId,
                    ct);

                // Even if no rows, that's a valid 200 with empty list.
                return Ok(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[FlowExecutions] Error in GetRunTimeline for BusinessId={BusinessId}, RunId={RunId}",
                    businessId,
                    runId);

                return StatusCode(500, "Failed to fetch run timeline.");
            }
        }
    }
}
