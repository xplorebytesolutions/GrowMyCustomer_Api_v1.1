// 📄 xbytechat-api/Features/ChatInbox/Controllers/ChatInboxController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Services;

namespace xbytechat.api.Features.ChatInbox.Controllers
{
    [ApiController]
    [Route("api/chat-inbox")]
    [Authorize]
    public sealed class ChatInboxController : ControllerBase
    {
        private readonly IChatInboxQueryService _queryService;
        private readonly IChatInboxCommandService _commandService;
        private readonly IChatInboxAssignmentService _assignmentService;
        private readonly ILogger<ChatInboxController> _logger;

        public ChatInboxController(
            IChatInboxQueryService queryService,
            IChatInboxCommandService commandService,
            IChatInboxAssignmentService assignmentService,
            ILogger<ChatInboxController> logger)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("conversations")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxConversationDto>), 200)]
        public async Task<IActionResult> GetConversations(
            [FromQuery] Guid businessId,
            [FromQuery] Guid? currentUserId, // kept for backward compatibility; ignored (token wins)
            [FromQuery] string? tab,
            [FromQuery] string? numberId,
            [FromQuery] string? search,
            [FromQuery] int? limit,
            [FromQuery] bool paged = false,
            [FromQuery] string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

            var tokenUserId = GetUserId();
            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

            var filter = new ChatInboxFilterDto
            {
                BusinessId = businessId,
                CurrentUserId = tokenUserId.Value, // ✅ token wins
                Tab = tab,
                NumberId = string.IsNullOrWhiteSpace(numberId) ? null : numberId,
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
                Limit = limit.GetValueOrDefault(50),
                Cursor = cursor
            };

            if (!paged)
            {
                var result = await _queryService.GetConversationsAsync(filter, cancellationToken);
                return Ok(result);
            }

            var page = await _queryService.GetConversationsPageAsync(filter, cancellationToken);
            return Ok(page);
        }

        [HttpGet("agents")]
        [ProducesResponseType(typeof(List<AgentDto>), 200)]
        public async Task<IActionResult> GetAgents(
            [FromQuery] Guid businessId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

            try
            {
                var rows = await _assignmentService.GetAgentsAsync(businessId, ct);
                return Ok(rows);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("messages")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxMessageDto>), 200)]
        public async Task<IActionResult> GetMessages(
            [FromQuery] Guid businessId,
            [FromQuery] Guid? contactId,
            [FromQuery] string? contactPhone,
            [FromQuery] int limit = 50,
            [FromQuery] bool paged = false,
            [FromQuery] string? cursor = null,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

            var tokenUserId = GetUserId();
            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

            var uid = tokenUserId.Value;

            // ✅ Prefer ContactId path
            if (contactId.HasValue && contactId.Value != Guid.Empty)
            {
                if (!paged)
                {
                    // ✅ SECURED overload (enforces AssignedOnly visibility)
                    var messages = await _queryService.GetMessagesForConversationByContactIdAsync(
                        businessId, contactId.Value, limit, uid, ct);

                    return Ok(messages);
                }

                // ✅ SECURED overload (enforces AssignedOnly visibility)
                var page = await _queryService.GetMessagesPageForConversationByContactIdAsync(
                    businessId, contactId.Value, limit, cursor, uid, ct);

                return Ok(page);
            }

            // Fallback: by phone
            if (string.IsNullOrWhiteSpace(contactPhone))
                return BadRequest("Provide either contactId or contactPhone.");

            if (!paged)
            {
                // ✅ SECURED overload (enforces AssignedOnly visibility)
                var byPhone = await _queryService.GetMessagesForConversationAsync(
                    businessId, contactPhone, limit, uid, ct);

                return Ok(byPhone);
            }

            // ✅ SECURED overload (enforces AssignedOnly visibility)
            var byPhonePage = await _queryService.GetMessagesPageForConversationByPhoneAsync(
                businessId, contactPhone, limit, cursor, uid, ct);

            return Ok(byPhonePage);
        }

        [HttpPost("send-message")]
        [ProducesResponseType(typeof(ChatInboxMessageDto), 200)]
        public async Task<ActionResult<ChatInboxMessageDto>> SendMessage(
            [FromBody] ChatInboxSendMessageRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null) return BadRequest("Request body is required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

            var tokenUserId = GetUserId();
            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

            // ✅ Critical: server-side actor identity
            request.ActorUserId = tokenUserId.Value;

            try
            {
                var result = await _commandService.SendAgentMessageAsync(request, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Forbidden chat-inbox send-message. BusinessId={BusinessId} ActorUserId={ActorUserId} ConversationId={ConversationId} ContactId={ContactId}",
                    request.BusinessId,
                    tokenUserId.Value,
                    request.ConversationId,
                    request.ContactId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //[HttpPost("mark-read")]
        //[ProducesResponseType(204)]
        //public async Task<IActionResult> MarkRead(
        //    [FromBody] ChatInboxMarkReadRequestDto request,
        //    CancellationToken ct = default)
        //{
        //    if (request == null) return BadRequest("Request body is required.");
        //    if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
        //        return BadRequest("BusinessId and ContactId are required.");

        //    // ✅ BusinessId MUST match token (security boundary)
        //    var tokenBiz = GetBusinessId();
        //    if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
        //    if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

        //    // ✅ UserId MUST come from token (never trust client)
        //    var tokenUserId = GetUserId();
        //    if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

        //    // Backward compatible: if client sent UserId, we ignore it and force token user.
        //    request.UserId = tokenUserId.Value;

        //    try
        //    {
        //        await _commandService.MarkConversationAsReadAsync(request, ct);
        //        return NoContent();
        //    }
        //    catch (UnauthorizedAccessException ex)
        //    {
        //        _logger.LogWarning(
        //            ex,
        //            "Forbidden chat-inbox mark-read. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId}",
        //            request.BusinessId,
        //            tokenUserId.Value,
        //            request.ContactId);

        //        return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        //    }
        //    catch (ArgumentException ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //}
        [HttpPost("mark-read")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> MarkRead(
    [FromBody] ChatInboxMarkReadRequestDto request,
    CancellationToken ct = default)
        {
            if (request == null) return BadRequest("Request body is required.");
            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
                return BadRequest("BusinessId and ContactId are required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

            var tokenUserId = GetUserId();
            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

            await _commandService.MarkConversationAsReadAsync(
                request.BusinessId,
                request.ContactId,
                tokenUserId.Value,          // ✅ token wins
                request.LastReadAtUtc,
                ct);

            return NoContent();
        }



        [HttpPost("assign")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Assign(
            [FromBody] AssignConversationDto request,
            CancellationToken ct = default)
        {
            if (request == null) return BadRequest("Request body is required.");

            if (request.BusinessId == Guid.Empty ||
                request.ContactId == Guid.Empty ||
                request.UserId == Guid.Empty)
            {
                return BadRequest("BusinessId, ContactId and UserId are required.");
            }

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

            try
            {
                await _assignmentService.AssignAsync(
                    request.BusinessId,
                    request.ContactId,
                    request.UserId,
                    actorUserId.Value,
                    ct);

                var updated = await TryGetConversationAsync(
                    request.BusinessId,
                    request.ContactId,
                    actorUserId.Value,
                    ct);

                return Ok(new { success = true, conversation = updated });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Forbidden chat-inbox assign. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId} AssigneeUserId={AssigneeUserId}",
                    request.BusinessId,
                    actorUserId.Value,
                    request.ContactId,
                    request.UserId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("unassign")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Unassign(
            [FromBody] UnassignConversationDto request,
            CancellationToken ct = default)
        {
            if (request == null) return BadRequest("Request body is required.");

            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
                return BadRequest("BusinessId and ContactId are required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

            try
            {
                await _assignmentService.UnassignAsync(
                    request.BusinessId,
                    request.ContactId,
                    actorUserId.Value,
                    ct);

                var updated = await TryGetConversationAsync(
                    request.BusinessId,
                    request.ContactId,
                    actorUserId.Value,
                    ct);

                return Ok(new { success = true, conversation = updated });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Forbidden chat-inbox unassign. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId}",
                    request.BusinessId,
                    actorUserId.Value,
                    request.ContactId);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("set-status")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SetStatus(
            [FromBody] SetConversationStatusDto request,
            CancellationToken ct = default)
        {
            if (request == null) return BadRequest("Request body is required.");

            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
                return BadRequest("BusinessId and ContactId are required.");

            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

            var actorUserId = GetUserId();
            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

            try
            {
                await _assignmentService.SetStatusAsync(
                    request.BusinessId,
                    request.ContactId,
                    request.Status,
                    actorUserId.Value,
                    ct);

                var updated = await TryGetConversationAsync(
                    request.BusinessId,
                    request.ContactId,
                    actorUserId.Value,
                    ct);

                return Ok(new { success = true, conversation = updated });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Forbidden chat-inbox set-status. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId} Status={Status}",
                    request.BusinessId,
                    actorUserId.Value,
                    request.ContactId,
                    request.Status);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private Guid? GetBusinessId()
        {
            var raw = User.FindFirstValue("businessId") ?? User.FindFirstValue("BusinessId");
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private Guid? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private async Task<ChatInboxConversationDto?> TryGetConversationAsync(
            Guid businessId,
            Guid contactId,
            Guid currentUserId,
            CancellationToken ct)
        {
            var rows = await _queryService.GetConversationsAsync(
                    new ChatInboxFilterDto
                    {
                        BusinessId = businessId,
                        CurrentUserId = currentUserId,
                        ContactId = contactId,
                        Limit = 1
                    },
                    ct)
                .ConfigureAwait(false);

            return rows.FirstOrDefault();
        }
    }
}


//// 📄 xbytechat-api/Features/ChatInbox/Controllers/ChatInboxController.cs
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using xbytechat.api.Features.ChatInbox.DTOs;
//using xbytechat.api.Features.ChatInbox.Services;

//namespace xbytechat.api.Features.ChatInbox.Controllers
//{
//    [ApiController]
//    [Route("api/chat-inbox")]
//    [Authorize]
//    public sealed class ChatInboxController : ControllerBase
//    {
//        private readonly IChatInboxQueryService _queryService;
//        private readonly IChatInboxCommandService _commandService;
//        private readonly IChatInboxAssignmentService _assignmentService;
//        private readonly ILogger<ChatInboxController> _logger;

//        public ChatInboxController(
//            IChatInboxQueryService queryService,
//            IChatInboxCommandService commandService,
//            IChatInboxAssignmentService assignmentService,
//            ILogger<ChatInboxController> logger)
//        {
//            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
//            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
//            _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        [HttpGet("conversations")]
//        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxConversationDto>), 200)]
//        public async Task<IActionResult> GetConversations(
//            [FromQuery] Guid businessId,
//            [FromQuery] Guid? currentUserId, // kept for backward compatibility; ignored (token wins)
//            [FromQuery] string? tab,
//            [FromQuery] string? numberId,
//            [FromQuery] string? search,
//            [FromQuery] int? limit,
//            [FromQuery] bool paged = false,
//            [FromQuery] string? cursor = null,
//            CancellationToken cancellationToken = default)
//        {
//            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

//            var tokenUserId = GetUserId();
//            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

//            var filter = new ChatInboxFilterDto
//            {
//                BusinessId = businessId,
//                CurrentUserId = tokenUserId.Value, // ✅ token wins
//                Tab = tab,
//                NumberId = string.IsNullOrWhiteSpace(numberId) ? null : numberId,
//                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
//                Limit = limit.GetValueOrDefault(50),
//                Cursor = cursor
//            };

//            if (!paged)
//            {
//                var result = await _queryService.GetConversationsAsync(filter, cancellationToken);
//                return Ok(result);
//            }

//            var page = await _queryService.GetConversationsPageAsync(filter, cancellationToken);
//            return Ok(page);
//        }

//        [HttpGet("agents")]
//        [ProducesResponseType(typeof(List<AgentDto>), 200)]
//        public async Task<IActionResult> GetAgents(
//            [FromQuery] Guid businessId,
//            CancellationToken ct = default)
//        {
//            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

//            try
//            {
//                var rows = await _assignmentService.GetAgentsAsync(businessId, ct);
//                return Ok(rows);
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        [HttpGet("messages")]
//        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxMessageDto>), 200)]
//        public async Task<IActionResult> GetMessages(
//            [FromQuery] Guid businessId,
//            [FromQuery] Guid? contactId,
//            [FromQuery] string? contactPhone,
//            [FromQuery] int limit = 50,
//            [FromQuery] bool paged = false,
//            [FromQuery] string? cursor = null,
//            CancellationToken ct = default)
//        {
//            if (businessId == Guid.Empty) return BadRequest("businessId is required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != businessId) return Forbid("businessId does not match your tenant.");

//            var tokenUserId = GetUserId();
//            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

//            var uid = tokenUserId.Value;

//            // ✅ Prefer ContactId path
//            if (contactId.HasValue && contactId.Value != Guid.Empty)
//            {
//                if (!paged)
//                {
//                    // ✅ SECURED overload (enforces AssignedOnly visibility)
//                    var messages = await _queryService.GetMessagesForConversationByContactIdAsync(
//                        businessId, contactId.Value, limit, uid, ct);

//                    return Ok(messages);
//                }

//                // ✅ SECURED overload (enforces AssignedOnly visibility)
//                var page = await _queryService.GetMessagesPageForConversationByContactIdAsync(
//                    businessId, contactId.Value, limit, cursor, uid, ct);

//                return Ok(page);
//            }

//            // Fallback: by phone
//            if (string.IsNullOrWhiteSpace(contactPhone))
//                return BadRequest("Provide either contactId or contactPhone.");

//            if (!paged)
//            {
//                // ✅ SECURED overload (enforces AssignedOnly visibility)
//                var byPhone = await _queryService.GetMessagesForConversationAsync(
//                    businessId, contactPhone, limit, uid, ct);

//                return Ok(byPhone);
//            }

//            // ✅ SECURED overload (enforces AssignedOnly visibility)
//            var byPhonePage = await _queryService.GetMessagesPageForConversationByPhoneAsync(
//                businessId, contactPhone, limit, cursor, uid, ct);

//            return Ok(byPhonePage);
//        }

//        [HttpPost("send-message")]
//        [ProducesResponseType(typeof(ChatInboxMessageDto), 200)]
//        public async Task<ActionResult<ChatInboxMessageDto>> SendMessage(
//            [FromBody] ChatInboxSendMessageRequestDto request,
//            CancellationToken ct = default)
//        {
//            if (request == null) return BadRequest("Request body is required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

//            var tokenUserId = GetUserId();
//            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

//            // ✅ Critical: server-side actor identity
//            request.ActorUserId = tokenUserId.Value;

//            try
//            {
//                var result = await _commandService.SendAgentMessageAsync(request, ct);
//                return Ok(result);
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                _logger.LogWarning(
//                    ex,
//                    "Forbidden chat-inbox send-message. BusinessId={BusinessId} ActorUserId={ActorUserId} ConversationId={ConversationId} ContactId={ContactId}",
//                    request.BusinessId,
//                    tokenUserId.Value,
//                    request.ConversationId,
//                    request.ContactId);
//                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
//            }
//            catch (ArgumentException ex)
//            {
//                return BadRequest(ex.Message);
//            }
//            catch (InvalidOperationException ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        [HttpPost("mark-read")]
//        [ProducesResponseType(204)]
//        public async Task<IActionResult> MarkRead(
//            [FromBody] ChatInboxMarkReadRequestDto request,
//            CancellationToken ct = default)
//        {
//            if (request == null) return BadRequest("Request body is required.");

//            if (request.BusinessId == Guid.Empty ||
//                request.ContactId == Guid.Empty ||
//                request.UserId == Guid.Empty)
//            {
//                return BadRequest("BusinessId, ContactId and UserId are required.");
//            }

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

//            var tokenUserId = GetUserId();
//            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");
//            if (tokenUserId.Value != request.UserId) return Forbid("userId does not match your session.");

//            await _commandService.MarkConversationAsReadAsync(request, ct);
//            return NoContent();
//        }

//        [HttpPost("assign")]
//        [ProducesResponseType(200)]
//        public async Task<IActionResult> Assign(
//            [FromBody] AssignConversationDto request,
//            CancellationToken ct = default)
//        {
//            if (request == null) return BadRequest("Request body is required.");

//            if (request.BusinessId == Guid.Empty ||
//                request.ContactId == Guid.Empty ||
//                request.UserId == Guid.Empty)
//            {
//                return BadRequest("BusinessId, ContactId and UserId are required.");
//            }

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

//            var actorUserId = GetUserId();
//            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

//            try
//            {
//                await _assignmentService.AssignAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    request.UserId,
//                    actorUserId.Value,
//                    ct);

//                var updated = await TryGetConversationAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    actorUserId.Value,
//                    ct);

//                return Ok(new { success = true, conversation = updated });
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                _logger.LogWarning(
//                    ex,
//                    "Forbidden chat-inbox assign. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId} AssigneeUserId={AssigneeUserId}",
//                    request.BusinessId,
//                    actorUserId.Value,
//                    request.ContactId,
//                    request.UserId);
//                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
//            }
//            catch (InvalidOperationException ex)
//            {
//                return NotFound(ex.Message);
//            }
//            catch (ArgumentException ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        [HttpPost("unassign")]
//        [ProducesResponseType(200)]
//        public async Task<IActionResult> Unassign(
//            [FromBody] UnassignConversationDto request,
//            CancellationToken ct = default)
//        {
//            if (request == null) return BadRequest("Request body is required.");

//            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
//                return BadRequest("BusinessId and ContactId are required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

//            var actorUserId = GetUserId();
//            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

//            try
//            {
//                await _assignmentService.UnassignAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    actorUserId.Value,
//                    ct);

//                var updated = await TryGetConversationAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    actorUserId.Value,
//                    ct);

//                return Ok(new { success = true, conversation = updated });
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                _logger.LogWarning(
//                    ex,
//                    "Forbidden chat-inbox unassign. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId}",
//                    request.BusinessId,
//                    actorUserId.Value,
//                    request.ContactId);
//                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
//            }
//            catch (InvalidOperationException ex)
//            {
//                return NotFound(ex.Message);
//            }
//            catch (ArgumentException ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        [HttpPost("set-status")]
//        [ProducesResponseType(200)]
//        public async Task<IActionResult> SetStatus(
//            [FromBody] SetConversationStatusDto request,
//            CancellationToken ct = default)
//        {
//            if (request == null) return BadRequest("Request body is required.");

//            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
//                return BadRequest("BusinessId and ContactId are required.");

//            var tokenBiz = GetBusinessId();
//            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");
//            if (tokenBiz.Value != request.BusinessId) return Forbid("businessId does not match your tenant.");

//            var actorUserId = GetUserId();
//            if (!actorUserId.HasValue) return Unauthorized("userId missing in token.");

//            try
//            {
//                await _assignmentService.SetStatusAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    request.Status,
//                    actorUserId.Value,
//                    ct);

//                var updated = await TryGetConversationAsync(
//                    request.BusinessId,
//                    request.ContactId,
//                    actorUserId.Value,
//                    ct);

//                return Ok(new { success = true, conversation = updated });
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                _logger.LogWarning(
//                    ex,
//                    "Forbidden chat-inbox set-status. BusinessId={BusinessId} ActorUserId={ActorUserId} ContactId={ContactId} Status={Status}",
//                    request.BusinessId,
//                    actorUserId.Value,
//                    request.ContactId,
//                    request.Status);
//                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
//            }
//            catch (InvalidOperationException ex)
//            {
//                return NotFound(ex.Message);
//            }
//            catch (ArgumentException ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        private Guid? GetBusinessId()
//        {
//            var raw = User.FindFirstValue("businessId") ?? User.FindFirstValue("BusinessId");
//            return Guid.TryParse(raw, out var id) ? id : null;
//        }

//        private Guid? GetUserId()
//        {
//            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
//            return Guid.TryParse(raw, out var id) ? id : null;
//        }

//        private async Task<ChatInboxConversationDto?> TryGetConversationAsync(
//            Guid businessId,
//            Guid contactId,
//            Guid currentUserId,
//            CancellationToken ct)
//        {
//            var rows = await _queryService.GetConversationsAsync(
//                    new ChatInboxFilterDto
//                    {
//                        BusinessId = businessId,
//                        CurrentUserId = currentUserId,
//                        ContactId = contactId,
//                        Limit = 1
//                    },
//                    ct)
//                .ConfigureAwait(false);

//            return rows.FirstOrDefault();
//        }
//    }
//}
