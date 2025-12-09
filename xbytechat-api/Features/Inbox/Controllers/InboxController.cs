using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Helpers;
using Microsoft.AspNetCore.Authorization;
using xbytechat.api.Shared;
using Microsoft.AspNetCore.SignalR;
using xbytechat.api.Features.Inbox.Hubs;

namespace xbytechat.api.Features.Inbox.Controllers
{
    [ApiController]
    [Route("api/inbox")]
    public class InboxController : ControllerBase
    {
        private readonly IInboxService _inboxService;
        private readonly IHubContext<InboxHub> _hubContext; // ✅ for SignalR push
        private readonly IUnreadCountService _unreadCountService;

        public InboxController(
            IInboxService inboxService,
            IHubContext<InboxHub> hubContext,
            IUnreadCountService unreadCountService)
        {
            _inboxService = inboxService;
            _hubContext = hubContext;
            _unreadCountService = unreadCountService;
        }

        /// <summary>
        /// Send a new message from UI or system.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] InboxMessageDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.MessageBody))
                return BadRequest("Message content is required.");

            var result = await _inboxService.SaveOutgoingMessageAsync(dto);
            return Ok(result);
        }

        /// <summary>
        /// Receive a message from external source (e.g., WhatsApp webhook).
        /// </summary>
        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveMessage([FromBody] InboxMessageDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.MessageBody))
                return BadRequest("Incoming message content is required.");

            // ✅ Ensure IDs are present
            if (dto.BusinessId == Guid.Empty || dto.ContactId == Guid.Empty)
                return BadRequest("BusinessId and ContactId are required.");

            // 1) Persist the inbound message
            var result = await _inboxService.SaveIncomingMessageAsync(dto);

            // 2) Push the real-time message to everyone in this business
            var groupName = $"business_{dto.BusinessId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveInboxMessage", new
            {
                contactId = dto.ContactId,
                messageContent = dto.MessageBody,  // ✅ aligned with frontend
                from = dto.RecipientPhone,
                status = "Delivered",
                sentAt = DateTime.UtcNow,
                isIncoming = true
            });

            // 3) Tell clients to refresh their own unread snapshot (per-user)
            //    We cannot compute per-user unread here (no userId in webhook context),
            //    so we emit a refresh signal that clients handle by calling GET /inbox/unread-counts.
            await _hubContext.Clients.Group(groupName)
                .SendAsync("UnreadCountChanged", new { refresh = true });

            return Ok(result);
        }

        /// <summary>
        /// Fetch message history between agent and customer using business token + contactId.
        /// </summary>
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessagesByContact([FromQuery] Guid contactId)
        {
            if (contactId == Guid.Empty)
                return BadRequest("ContactId is required.");

            var businessId = User.GetBusinessId();
            var messages = await _inboxService.GetMessagesByContactAsync(businessId, contactId);
            return Ok(messages);
        }

        [HttpGet("conversation")]
        public async Task<IActionResult> GetConversation(
            [FromQuery] Guid businessId,
            [FromQuery] string userPhone,
            [FromQuery] string contactPhone)
        {
            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(userPhone) || string.IsNullOrWhiteSpace(contactPhone))
                return BadRequest("Invalid input.");

            var messages = await _inboxService.GetConversationAsync(businessId, userPhone, contactPhone);
            return Ok(messages);
        }

        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkMessagesAsRead([FromQuery] Guid contactId)
        {
            if (contactId == Guid.Empty)
                return BadRequest("ContactId is required.");

            var businessId = User.GetBusinessId();
            await _inboxService.MarkMessagesAsReadAsync(businessId, contactId);
            return Ok();
        }

        [HttpGet("unread-counts")]
        public async Task<IActionResult> GetUnreadCounts()
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (businessId == null || userId == null)
                return Unauthorized();

            var counts = await _unreadCountService.GetUnreadCountsAsync(businessId, userId);
            return Ok(counts);
        }
    }
}

//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Threading.Tasks;
//using xbytechat.api.Features.Inbox.DTOs;
//using xbytechat.api.Features.Inbox.Services;
//using xbytechat.api.Helpers;
//using Microsoft.AspNetCore.Authorization;
//using xbytechat.api.Shared;
//using Microsoft.AspNetCore.SignalR;
//using xbytechat.api.Features.Inbox.Hubs;
//namespace xbytechat.api.Features.Inbox.Controllers
//{
//    [ApiController]
//    [Route("api/inbox")]
//    public class InboxController : ControllerBase
//    {
//        private readonly IInboxService _inboxService;
//        private readonly IHubContext<InboxHub> _hubContext; // ✅ for SignalR push
//        private readonly IUnreadCountService _unreadCountService;
//        public InboxController(IInboxService inboxService, IHubContext<InboxHub> hubContext, IUnreadCountService unreadCountService)
//        {
//            _inboxService = inboxService;
//            _hubContext = hubContext;
//            _unreadCountService = unreadCountService;   
//        }

//        /// <summary>
//        /// Send a new message from UI or system.
//        /// </summary>
//        [HttpPost("send")]
//        public async Task<IActionResult> SendMessage([FromBody] InboxMessageDto dto)
//        {
//            if (dto == null || string.IsNullOrWhiteSpace(dto.MessageBody))
//                return BadRequest("Message content is required.");

//            var result = await _inboxService.SaveOutgoingMessageAsync(dto);
//            return Ok(result);
//        }

//        /// <summary>
//        /// Receive a message from external source (e.g., WhatsApp webhook).
//        /// </summary>
//        [HttpPost("receive")]
//        public async Task<IActionResult> ReceiveMessage([FromBody] InboxMessageDto dto)
//        {
//            if (dto == null || string.IsNullOrWhiteSpace(dto.MessageBody))
//                return BadRequest("Incoming message content is required.");

//            var result = await _inboxService.SaveIncomingMessageAsync(dto);

//            // ✅ Also broadcast in real-time to clients in this business group
//            var groupName = $"business_{dto.BusinessId}";
//            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveInboxMessage", new
//            {
//                contactId = dto.ContactId,
//                messageContent = dto.MessageBody,  // ✅ aligned with frontend
//                from = dto.RecipientPhone,
//                status = "Delivered",
//                sentAt = DateTime.UtcNow,
//                isIncoming = true
//            });

//            return Ok(result);
//        }

//        /// <summary>
//        /// Fetch message history between agent and customer using business token + contactId.
//        /// </summary>
//        [HttpGet("messages")]
//        public async Task<IActionResult> GetMessagesByContact([FromQuery] Guid contactId)
//        {
//            if (contactId == Guid.Empty)
//                return BadRequest("ContactId is required.");

//            var businessId = User.GetBusinessId();
//            var messages = await _inboxService.GetMessagesByContactAsync(businessId, contactId);
//            return Ok(messages);
//        }

//        [HttpGet("conversation")]
//        public async Task<IActionResult> GetConversation(
//            [FromQuery] Guid businessId,
//            [FromQuery] string userPhone,
//            [FromQuery] string contactPhone)
//        {
//            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(userPhone) || string.IsNullOrWhiteSpace(contactPhone))
//                return BadRequest("Invalid input.");

//            var messages = await _inboxService.GetConversationAsync(businessId, userPhone, contactPhone);
//            return Ok(messages);
//        }

//        [HttpPost("mark-read")]
//        public async Task<IActionResult> MarkMessagesAsRead([FromQuery] Guid contactId)
//        {
//            if (contactId == Guid.Empty)
//                return BadRequest("ContactId is required.");

//            var businessId = User.GetBusinessId();
//            await _inboxService.MarkMessagesAsReadAsync(businessId, contactId);
//            return Ok();
//        }


//        [HttpGet("unread-counts")]
//        public async Task<IActionResult> GetUnreadCounts()
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();

//            if (businessId == null || userId == null)
//                return Unauthorized();

//            var counts = await _unreadCountService.GetUnreadCountsAsync(
//                businessId, userId);

//            return Ok(counts);
//        }
//    }
//}



