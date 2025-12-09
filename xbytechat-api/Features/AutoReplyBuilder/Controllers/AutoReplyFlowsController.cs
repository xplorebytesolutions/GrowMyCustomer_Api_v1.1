using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.AutoReplyBuilder.Controllers
{
    /// <summary>
    /// Controller for AutoReply builder operations:
    /// - CRUD for AutoReply flows used by the canvas UI
    /// - Test-match endpoint used by the "Test Auto-Reply Match" panel
    /// </summary>
    [ApiController]
    [Route("api/autoreplyflows")]
    [Authorize]
    public sealed class AutoReplyFlowsController : ControllerBase
    {
        private readonly IAutoReplyFlowService _service;
        private readonly IAutoReplyRuntimeService _runtime;
        private readonly ILogger<AutoReplyFlowsController> _logger;

        public AutoReplyFlowsController(
            IAutoReplyFlowService service,
            IAutoReplyRuntimeService runtime,
            ILogger<AutoReplyFlowsController> logger)
        {
            _service = service;
            _runtime = runtime;
            _logger = logger;
        }

        // ---------------------------------------------------------
        // 1) LIST FLOWS  - used when the builder page loads
        // GET /api/autoreplyflows
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AutoReplyFlowSummaryDto>>> GetAll(
            CancellationToken ct)
        {
            var bizId = ClaimsBusinessDetails.GetBusinessId(User);
            var flows = await _service.GetFlowsForBusinessAsync(bizId, ct);
            return Ok(flows);
        }

        // ---------------------------------------------------------
        // 2) GET SINGLE FLOW - used when opening a specific flow
        // GET /api/autoreplyflows/{id}
        // ---------------------------------------------------------
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AutoReplyFlowDto>> Get(Guid id, CancellationToken ct)
        {
            var bizId = ClaimsBusinessDetails.GetBusinessId(User);
            var flow = await _service.GetFlowAsync(bizId, id, ct);
            if (flow is null) return NotFound();
            return Ok(flow);
        }

        // ---------------------------------------------------------
        // 3) CREATE / UPDATE FLOW - used when you click Save in builder
        // POST /api/autoreplyflows
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<ActionResult<AutoReplyFlowDto>> Save(
            [FromBody] AutoReplyFlowDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("AutoReplyFlow model invalid: {@ModelState}", ModelState);
                return ValidationProblem(ModelState);
            }

            var bizId = ClaimsBusinessDetails.GetBusinessId(User);
            var saved = await _service.SaveFlowAsync(bizId, dto, ct);
            return Ok(saved);
        }

        // ---------------------------------------------------------
        // 3a) BACK-COMPAT ALIAS FOR OLDER FRONTEND
        // POST /api/autoreplyflows/save
        // ---------------------------------------------------------
        [HttpPost("save")]
        public Task<ActionResult<AutoReplyFlowDto>> SaveAlias(
            [FromBody] AutoReplyFlowDto dto,
            CancellationToken ct)
            => Save(dto, ct);

        // ---------------------------------------------------------
        // 4) DELETE FLOW
        // DELETE /api/autoreplyflows/{id}
        // ---------------------------------------------------------
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var bizId = ClaimsBusinessDetails.GetBusinessId(User);
            await _service.DeleteFlowAsync(bizId, id, ct);
            return NoContent();
        }
        // ---------------------------------------------------------
        // 4a) UPDATE FLOW STATUS (ACTIVE / INACTIVE)
        // PATCH /api/autoreplyflows/{id}/status
        // ---------------------------------------------------------
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] AutoReplyFlowStatusUpdateDto dto,
            CancellationToken ct)
        {
            if (dto is null)
            {
                return BadRequest("Request body is required.");
            }

            var bizId = ClaimsBusinessDetails.GetBusinessId(User);

            await _service.SetActiveAsync(
                bizId,
                id,
                dto.IsActive,
                ct);

            return NoContent();
        }

        // ---------------------------------------------------------
        // 5) TEST-MATCH ENDPOINT FOR BUILDER PANEL
        // POST /api/autoreplyflows/test-match
        //
        // Frontend sends: { businessId, incomingText }
        // Response: { isMatch, flowId, flowName, matchedKeyword, startNodeType, startNodeName }
        // ---------------------------------------------------------
        [HttpPost("test-match")]
        public async Task<ActionResult<AutoReplyTestMatchResponseDto>> TestMatchAsync(
            [FromBody] AutoReplyTestMatchRequestDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            _logger.LogInformation(
                "🔍 AutoReply test-match requested for Business {BusinessId} with text: {Text}",
                dto.BusinessId,
                dto.IncomingText
            );

            // NOTE: Right now the runtime is mostly stubbed.
            // This call will later contain the real keyword/flow matching logic.
            var result = await _runtime.TestMatchAsync(dto.BusinessId, dto.IncomingText, ct);

            var response = new AutoReplyTestMatchResponseDto
            {
                IsMatch = result.Handled,
                FlowId = result.AutoReplyFlowId ?? result.CtaFlowConfigId,
                FlowName = null, // will be filled when runtime returns metadata
                MatchedKeyword = result.MatchedKeyword,
                StartNodeType = null, // will be wired from CTAFlow metadata later
                StartNodeName = null
            };

            return Ok(response);
        }
    }
}


//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using xbytechat.api.Features.AutoReplyBuilder.DTOs;
//using xbytechat.api.Features.AutoReplyBuilder.Services;

//namespace xbytechat.api.Features.AutoReplyBuilder.Controllers
//{
//    /// <summary>
//    /// Controller for AutoReply builder operations (test match, later maybe listing flows, etc.).
//    /// </summary>
//    [ApiController]
//    [Route("api/autoreplyflows")]
//    public sealed class AutoReplyFlowsController : ControllerBase
//    {
//        private readonly IAutoReplyRuntimeService _autoReplyRuntime;
//        private readonly ILogger<AutoReplyFlowsController> _logger;

//        public AutoReplyFlowsController(
//            IAutoReplyRuntimeService autoReplyRuntime,
//            ILogger<AutoReplyFlowsController> logger)
//        {
//            _autoReplyRuntime = autoReplyRuntime;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Test whether a given incoming text would match any AutoReply rule.
//        /// Used by the AutoReplyBuilder "Test Auto-Reply Match" panel.
//        /// </summary>
//        [HttpPost("test-match")]
//        public async Task<ActionResult<AutoReplyTestMatchResponseDto>> TestMatchAsync(
//            [FromBody] AutoReplyTestMatchRequestDto dto,
//            CancellationToken ct)
//        {
//            if (!ModelState.IsValid)
//            {
//                return ValidationProblem(ModelState);
//            }

//            _logger.LogInformation(
//                "🔍 AutoReply test-match requested for Business {BusinessId} with text: {Text}",
//                dto.BusinessId,
//                dto.IncomingText
//            );

//            var result = await _autoReplyRuntime.TestMatchAsync(dto.BusinessId, dto.IncomingText, ct);

//            // For now, runtime always returns NotHandled, so IsMatch will be false.
//            // Later, when we implement matching, Handled = true will mean a rule matched.
//            var response = new AutoReplyTestMatchResponseDto
//            {
//                IsMatch = result.Handled,
//                FlowId = result.AutoReplyFlowId ?? result.CtaFlowConfigId,
//                FlowName = null, // will be populated once runtime can surface flow metadata
//                MatchedKeyword = result.MatchedKeyword,
//                StartNodeType = null, // to be filled when CTAFlow metadata is integrated
//                StartNodeName = null  // same as above
//            };

//            return Ok(response);
//        }
//    }
//}


////using Microsoft.AspNetCore.Authorization;
////using Microsoft.AspNetCore.Mvc;
////using xbytechat.api.Features.AutoReplyBuilder.DTOs;
////using xbytechat.api.Shared;
////using xbytechat.api.Features.AutoReplyBuilder.Services;
////using xbytechat.api.Features.AutoReplyBuilder.DTOs;

////namespace xbytechat.api.Features.AutoReplyBuilder.Controllers
////{
////    [ApiController]
////    [Route("api/autoreplyflows")]
////    [Authorize]
////    public sealed class AutoReplyFlowsController : ControllerBase
////    {
////        private readonly IAutoReplyFlowService _service;
////        private readonly IAutoReplyRuntimeService _runtime;
////        private readonly ILogger<AutoReplyFlowsController> _logger;

////        public AutoReplyFlowsController(
////            IAutoReplyFlowService service,
////            IAutoReplyRuntimeService runtime,
////            ILogger<AutoReplyFlowsController> logger)
////        {
////            _service = service;
////            _runtime = runtime;
////            _logger = logger;
////        }

////        // GET /api/autoreplyflows
////        [HttpGet]
////        public async Task<ActionResult<IEnumerable<AutoReplyFlowSummaryDto>>> GetAll(CancellationToken ct)
////        {
////            var bizId = User.GetBusinessId();
////            var flows = await _service.GetFlowsForBusinessAsync(bizId, ct);
////            return Ok(flows);
////        }

////        // GET /api/autoreplyflows/{id}
////        [HttpGet("{id:guid}")]
////        public async Task<ActionResult<AutoReplyFlowDto>> Get(Guid id, CancellationToken ct)
////        {
////            var bizId = User.GetBusinessId();
////            var flow = await _service.GetFlowAsync(bizId, id, ct);
////            if (flow is null) return NotFound();
////            return Ok(flow);
////        }

////        // POST /api/autoreplyflows (create or update)
////        [HttpPost]
////        public async Task<ActionResult<AutoReplyFlowDto>> Save([FromBody] AutoReplyFlowDto dto, CancellationToken ct)
////        {
////            if (!ModelState.IsValid)
////            {
////                _logger.LogWarning("AutoReplyFlow model invalid: {@ModelState}", ModelState);
////                return ValidationProblem(ModelState);
////            }

////            var bizId = User.GetBusinessId();
////            var saved = await _service.SaveFlowAsync(bizId, dto, ct);
////            return Ok(saved);
////        }

////        // Back-compat alias for older frontend
////        [HttpPost("save")]
////        public Task<ActionResult<AutoReplyFlowDto>> SaveAlias([FromBody] AutoReplyFlowDto dto, CancellationToken ct) => Save(dto, ct);

////        // DELETE /api/autoreplyflows/{id}
////        [HttpDelete("{id:guid}")]
////        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
////        {
////            var bizId = User.GetBusinessId();
////            await _service.DeleteFlowAsync(bizId, id, ct);
////            return NoContent();
////        }

////        // POST /api/autoreplyflows/test-match
////        [HttpPost("test-match")]
////        public async Task<ActionResult<AutoReplyMatchResultDto>> TestMatch([FromBody] AutoReplyMatchRequestDto request, CancellationToken ct)
////        {
////            var result = await _runtime.FindMatchAsync(request, ct);
////            return Ok(result);
////        }
////    }
////}
