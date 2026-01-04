using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Models;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Shared;
using xbytechat.api.Features.Inbox.DTOs;

namespace xbytechat.api.Features.Inbox.Hubs
{
    [Authorize]
    public sealed class InboxHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly IMessageEngineService _messageService;
        private readonly IUnreadCountService _unreadCountService;
        private readonly ILogger<InboxHub> _logger;

        public InboxHub(
            AppDbContext db,
            IMessageEngineService messageService,
            IUnreadCountService unreadCountService,
            ILogger<InboxHub> logger)
        {
            _db = db;
            _messageService = messageService;
            _unreadCountService = unreadCountService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            //var businessId = Context.User.GetBusinessId();
            var businessId = Context.User.ResolveBusinessId(Context.GetHttpContext()!);
            if (businessId == Guid.Empty)
            {
                _logger.LogWarning("InboxHub connect: missing BusinessId claim. Conn={ConnId}", Context.ConnectionId);
                await base.OnConnectedAsync();
                return;
            }

            var groupName = GetBusinessGroupName(businessId);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("InboxHub connected. Group={Group} Conn={ConnId} UserIdentifier={UserId}",
                groupName, Context.ConnectionId, Context.UserIdentifier);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
           // var businessId = Context.User.GetBusinessId();
            var businessId = Context.User.ResolveBusinessId(Context.GetHttpContext()!);
            if (businessId != Guid.Empty)
            {
                var groupName = GetBusinessGroupName(businessId);
                try { await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName); } catch { /* ignore */ }

                _logger.LogInformation("InboxHub disconnected. Group={Group} Conn={ConnId}", groupName, Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ✅ Frontend should invoke: connection.invoke("SendMessageToContact", { contactId, message })
        public async Task SendMessageToContact(SendMessageInputDto dto)
        {
            if (dto == null)
                return;

            if (dto.ContactId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Message))
            {
                _logger.LogWarning("SendMessageToContact invalid payload. ContactId={ContactId}", dto.ContactId);
                return;
            }

           // var businessId = Context.User.GetBusinessId();
            var businessId = Context.User.ResolveBusinessId(Context.GetHttpContext()!);
            var userId = Context.User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
            {
                _logger.LogWarning("SendMessageToContact missing BusinessId/UserId. Conn={ConnId}", Context.ConnectionId);
                return;
            }

            try
            {
                // ✅ Lookup recipient phone number from Contacts
                var contact = await _db.Contacts
                    .Where(c => c.BusinessId == businessId && c.Id == dto.ContactId)
                    .FirstOrDefaultAsync();

                if (contact == null || string.IsNullOrWhiteSpace(contact.PhoneNumber))
                {
                    _logger.LogWarning("SendMessageToContact contact not found or missing phone. BusinessId={BusinessId} ContactId={ContactId}",
                        businessId, dto.ContactId);

                    await Clients.Caller.SendAsync("ReceiveInboxMessage", new
                    {
                        contactId = dto.ContactId,
                        messageContent = dto.Message,
                        from = userId,
                        status = "Failed",
                        error = "Invalid contact"
                    });

                    return;
                }

                var sendDto = new TextMessageSendDto
                {
                    BusinessId = businessId,
                    ContactId = dto.ContactId,
                    RecipientNumber = contact.PhoneNumber,
                    TextContent = dto.Message
                };

                var result = await _messageService.SendTextDirectAsync(sendDto);

                var inboxMessage = new
                {
                    contactId = dto.ContactId,
                    messageContent = dto.Message,
                    from = userId,
                    status = result.Success ? "Sent" : "Failed",
                    sentAt = DateTime.UtcNow,
                    logId = result.LogId,
                    senderId = userId,
                    isIncoming = false
                };

                // Caller always gets it
                await Clients.Caller.SendAsync("ReceiveInboxMessage", inboxMessage);

                // Others in the same business get it
                var groupName = GetBusinessGroupName(businessId);
                await Clients.GroupExcept(groupName, Context.ConnectionId)
                    .SendAsync("ReceiveInboxMessage", inboxMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendMessageToContact failed. BusinessId={BusinessId} ContactId={ContactId}",
                    businessId, dto.ContactId);

                await Clients.Caller.SendAsync("ReceiveInboxMessage", new
                {
                    contactId = dto.ContactId,
                    messageContent = dto.Message,
                    from = userId,
                    status = "Failed",
                    error = "Server error"
                });
            }
        }

      
        public async Task MarkAsRead(Guid contactId)
        {
            if (contactId == Guid.Empty)
                return;

            var userId = Context.User.GetUserId();
           // var businessId = Context.User.GetBusinessId();
            var businessId = Context.User.ResolveBusinessId(Context.GetHttpContext()!);
            if (userId == Guid.Empty || businessId == Guid.Empty)
                return;

            try
            {
                var now = DateTime.UtcNow;

                // ✅ Upsert ContactRead
                var readEntry = await _db.ContactReads
                    .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.ContactId == contactId && r.UserId == userId);

                if (readEntry == null)
                {
                    _db.ContactReads.Add(new ContactRead
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        ContactId = contactId,
                        UserId = userId,
                        LastReadAt = now
                    });
                }
                else
                {
                    readEntry.LastReadAt = now;
                }

                await _db.SaveChangesAsync();

                // ✅ CRITICAL FIX:
                // UnreadCountService returns ONLY contacts with unread > 0.
                // If this contact becomes 0, it will be missing from the dictionary,
                // so the frontend would never clear the old badge.
                // Force-send an explicit "0" for this contact to the caller.
                await Clients.Caller.SendAsync("UnreadCountChanged", new
                {
                    contactId = contactId,
                    unreadCount = 0
                });

                // ✅ Caller also gets their full unread map (for other chats)
                var unreadCounts = await _unreadCountService.GetUnreadCountsAsync(businessId, userId);
                await Clients.Caller.SendAsync("UnreadCountChanged", unreadCounts);

                // ✅ Others get "refresh your own" signal
                var groupName = GetBusinessGroupName(businessId);
                await Clients.GroupExcept(groupName, Context.ConnectionId)
                    .SendAsync("UnreadCountChanged", new { refresh = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MarkAsRead failed. BusinessId={BusinessId} UserId={UserId} ContactId={ContactId}",
                    businessId, userId, contactId);
            }
        }

        private static string GetBusinessGroupName(Guid businessId) => $"business_{businessId}";
    }
}





