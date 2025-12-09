// 📄 xbytechat-api/Features/ChatInbox/Controllers/ChatInboxController.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Services;

namespace xbytechat.api.Features.ChatInbox.Controllers
{
    [ApiController]
    [Route("api/chat-inbox")]
    public sealed class ChatInboxController : ControllerBase
    {
        private readonly IChatInboxQueryService _queryService;
        private readonly IChatInboxCommandService _commandService;

        public ChatInboxController(
            IChatInboxQueryService queryService,
            IChatInboxCommandService commandService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        }

        // 🚩 Conversations list
        [HttpGet("conversations")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxConversationDto>), 200)]
        public async Task<IActionResult> GetConversations(
            [FromQuery] Guid businessId,
            [FromQuery] Guid? currentUserId,
            [FromQuery] string? tab,
            [FromQuery] string? numberId,
            [FromQuery] string? search,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            if (businessId == Guid.Empty)
            {
                return BadRequest("businessId is required.");
            }

            var filter = new ChatInboxFilterDto
            {
                BusinessId = businessId,
                CurrentUserId = currentUserId,
                Tab = tab,
                NumberId = string.IsNullOrWhiteSpace(numberId) ? null : numberId,
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
                Limit = limit.GetValueOrDefault(50),
            };

            switch (tab?.ToLowerInvariant())
            {
                case "unassigned":
                    filter.OnlyUnassigned = true;
                    break;
                case "my":
                    filter.OnlyAssignedToMe = true;
                    break;
            }

            var result = await _queryService.GetConversationsAsync(filter, cancellationToken);
            return Ok(result);
        }

        // 💬 Messages for a conversation
        [HttpGet("messages")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatInboxMessageDto>), 200)]
        public async Task<ActionResult<IReadOnlyList<ChatInboxMessageDto>>> GetMessages(
            [FromQuery] Guid businessId,
            [FromQuery] string contactPhone,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
            {
                return BadRequest("businessId is required.");
            }

            if (string.IsNullOrWhiteSpace(contactPhone))
            {
                return BadRequest("contactPhone is required.");
            }

            var messages = await _queryService.GetMessagesForConversationAsync(
                businessId,
                contactPhone,
                limit,
                ct);

            return Ok(messages);
        }

        // 📤 Send a message from agent → customer (used by Chat Inbox middle panel)
        [HttpPost("send-message")]
        [ProducesResponseType(typeof(ChatInboxMessageDto), 200)]
        public async Task<ActionResult<ChatInboxMessageDto>> SendMessage(
            [FromBody] ChatInboxSendMessageRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var result = await _commandService.SendAgentMessageAsync(request, ct);
            return Ok(result);
        }

        // ✅ Mark conversation as read for current user
        [HttpPost("mark-read")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> MarkRead(
            [FromBody] ChatInboxMarkReadRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.BusinessId == Guid.Empty ||
                request.ContactId == Guid.Empty ||
                request.UserId == Guid.Empty)
            {
                return BadRequest("BusinessId, ContactId and UserId are required.");
            }

            await _commandService.MarkConversationAsReadAsync(request, ct);
            return NoContent();
        }

        // 👤 Assign conversation to an agent
        [HttpPost("assign")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Assign(
            [FromBody] ChatInboxAssignRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.BusinessId == Guid.Empty ||
                request.ContactId == Guid.Empty ||
                request.UserId == Guid.Empty)
            {
                return BadRequest("BusinessId, ContactId and UserId are required.");
            }

            await _commandService.AssignConversationAsync(request, ct);
            return NoContent();
        }

        // 🚫 Unassign conversation
        [HttpPost("unassign")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Unassign(
            [FromBody] ChatInboxUnassignRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.BusinessId == Guid.Empty ||
                request.ContactId == Guid.Empty)
            {
                return BadRequest("BusinessId and ContactId are required.");
            }

            await _commandService.UnassignConversationAsync(request, ct);
            return NoContent();
        }

        [HttpPost("status")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> ChangeStatus(
            [FromBody] ChatInboxChangeStatusRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.BusinessId == Guid.Empty || request.ContactId == Guid.Empty)
            {
                return BadRequest("BusinessId and ContactId are required.");
            }

            await _commandService.ChangeConversationStatusAsync(request, ct);
            return NoContent();
        }
    }
}

