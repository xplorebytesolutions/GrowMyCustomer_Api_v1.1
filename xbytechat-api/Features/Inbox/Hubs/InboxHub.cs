// 📄 xbytechat.api/Features/Inbox/InboxHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Shared;
using xbytechat.api.Models;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Features.Inbox.Services;
using System;
using System.Linq;

namespace xbytechat.api.Features.Inbox.Hubs
{
    [Authorize]
    public class InboxHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly IMessageEngineService _messageService;
        private readonly IUnreadCountService _unreadCountService;

        public InboxHub(AppDbContext db, IMessageEngineService messageService, IUnreadCountService unreadCountService)
        {
            _db = db;
            _messageService = messageService;
            _unreadCountService = unreadCountService;
        }

        public override async Task OnConnectedAsync()
        {
            var businessId = Context.User.GetBusinessId(); // non-nullable Guid in your codebase

            if (businessId == Guid.Empty)
            {
                Console.WriteLine("❌ InboxHub connect: missing BusinessId claim, skipping group join.");
                await base.OnConnectedAsync();
                return;
            }

            var groupName = $"business_{businessId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"✅ Connected to group: {groupName}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var businessId = Context.User.GetBusinessId();
            if (businessId != Guid.Empty)
            {
                var groupName = $"business_{businessId}";
                try { await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName); } catch { /* no-op */ }
                Console.WriteLine($"⚪ Disconnected from group: {groupName} (conn: {Context.ConnectionId})");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessageToContact(SendMessageInputDto dto)
        {
            Console.WriteLine("📩 Raw DTO payload:");
            Console.WriteLine($"ContactId: {dto.ContactId}, Message: {dto.Message}");

            // Guid is non-nullable → compare to Guid.Empty
            if (dto.ContactId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Message))
            {
                Console.WriteLine("❌ Invalid contact or empty message.");
                return;
            }

            var businessId = Context.User.GetBusinessId();
            var userId = Context.User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
            {
                Console.WriteLine("❌ Missing BusinessId/UserId in hub context.");
                return;
            }

            // ✅ Lookup recipient phone number from Contact table
            var contact = await _db.Contacts
                .Where(c => c.BusinessId == businessId && c.Id == dto.ContactId)
                .FirstOrDefaultAsync();

            if (contact == null || string.IsNullOrWhiteSpace(contact.PhoneNumber))
            {
                Console.WriteLine($"❌ Contact not found or missing phone number. ContactId: {dto.ContactId}");
                await Clients.Caller.SendAsync("ReceiveInboxMessage", new
                {
                    contactId = dto.ContactId,
                    messageContent = dto.Message,   // aligned with frontend
                    from = userId,
                    status = "Failed",
                    error = "Invalid contact"
                });
                return;
            }

            // ✅ Prepare DTO for WhatsApp sending
            var sendDto = new TextMessageSendDto
            {
                BusinessId = businessId,
                ContactId = dto.ContactId,
                RecipientNumber = contact.PhoneNumber,
                TextContent = dto.Message
            };

            // 🚀 Send via WhatsApp API and save to MessageLogs
            var result = await _messageService.SendTextDirectAsync(sendDto);

            // ✅ Unified payload (outbound)
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

            // Sender
            await Clients.Caller.SendAsync("ReceiveInboxMessage", inboxMessage);

            // Others in business
            var groupName = $"business_{businessId}";
            await Clients.GroupExcept(groupName, Context.ConnectionId)
                .SendAsync("ReceiveInboxMessage", inboxMessage);
        }

        public async Task MarkAsRead(Guid contactId)
        {
            Console.WriteLine($"🟢 MarkAsRead triggered for ContactId: {contactId}");
            var userId = Context.User.GetUserId();
            var businessId = Context.User.GetBusinessId();

            if (userId == Guid.Empty || businessId == Guid.Empty) return;

            var now = DateTime.UtcNow;

            // Upsert ContactRead
            var readEntry = await _db.ContactReads
                .FirstOrDefaultAsync(r => r.ContactId == contactId && r.UserId == userId);

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

            // Per-agent unread snapshot
            var unreadCounts = await _unreadCountService.GetUnreadCountsAsync(businessId, userId);

            var groupName = $"business_{businessId}";

            // Send the caller their map…
            await Clients.User(userId.ToString())
                .SendAsync("UnreadCountChanged", unreadCounts);

            // …and signal others to refresh their own
            await Clients.GroupExcept(groupName, Context.ConnectionId)
                .SendAsync("UnreadCountChanged", new { refresh = true });
        }
    }
}



