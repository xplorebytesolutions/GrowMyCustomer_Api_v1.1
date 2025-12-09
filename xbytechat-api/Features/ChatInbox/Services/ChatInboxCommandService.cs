// 📄 xbytechat-api/Features/ChatInbox/Services/ChatInboxCommandService.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Models;

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// Default write-side handler for Chat Inbox actions.
    /// Delegates the actual send to the central MessagesEngine so that
    /// MessageLogs / provider calls stay consistent across the app.
    /// Also manages per-user read state (ContactReads) and assignment.
    /// </summary>
    public sealed class ChatInboxCommandService : IChatInboxCommandService
    {
        private readonly AppDbContext _db;
        private readonly IMessageEngineService _messageEngine;

        public ChatInboxCommandService(
            AppDbContext db,
            IMessageEngineService messageEngine)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _messageEngine = messageEngine ?? throw new ArgumentNullException(nameof(messageEngine));
        }

        public async Task<ChatInboxMessageDto> SendAgentMessageAsync(
            ChatInboxSendMessageRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(request));

            if (string.IsNullOrWhiteSpace(request.To))
                throw new ArgumentException("Target phone (To) is required.", nameof(request));

            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text is required.", nameof(request));

            var businessId = request.BusinessId;
            var phone = request.To.Trim();

            // 🔎 Resolve contact:
            Guid? contactId = request.ContactId;

            if (!contactId.HasValue)
            {
                var contact = await _db.Contacts
                    .AsNoTracking()
                    .Where(c => c.BusinessId == businessId && c.PhoneNumber == phone)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                contactId = contact?.Id;
            }

            var effectiveContactId = contactId ?? Guid.Empty;

            // 📨 Build the DTO expected by MessagesEngine text pipeline.
            var textDto = new TextMessageSendDto
            {
                BusinessId = businessId,
                RecipientNumber = phone,
                TextContent = request.Text,
                ContactId = effectiveContactId,
                PhoneNumberId = string.IsNullOrWhiteSpace(request.NumberId)
                    ? null
                    : request.NumberId.Trim(),
                Provider = null,         // let engine resolve default provider/number
                Source = "agent"         // so analytics can separate human replies
            };

            // 🧠 Delegate to the central MessagesEngine.
            var result = await _messageEngine
                .SendTextDirectAsync(textDto)
                .ConfigureAwait(false);

            // Try to load the MessageLog row so we can return a rich bubble DTO.
            MessageLog? log = null;
            if (result.LogId.HasValue)
            {
                log = await _db.MessageLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == result.LogId.Value, ct)
                    .ConfigureAwait(false);
            }

            var nowUtc = DateTime.UtcNow;
            var sentAt = nowUtc;
            var bubbleText = request.Text;
            string? status = null;
            string? errorMessage = null;
            Guid bubbleId;

            if (log != null)
            {
                bubbleId = log.Id;
                bubbleText = log.MessageContent ?? request.Text;

                var ts = log.SentAt ?? log.CreatedAt;
                sentAt = ts.Kind == DateTimeKind.Utc ? ts : ts.ToUniversalTime();

                status = log.Status;
                errorMessage = log.ErrorMessage;
            }
            else
            {
                bubbleId = Guid.NewGuid();
                sentAt = nowUtc;
                status = result.Success ? "Sent" : "Failed";
                errorMessage = result.Success ? null : result.Message;
            }

            // 🧱 Map to ChatInboxMessageDto so the UI can render the bubble immediately.
            var dto = new ChatInboxMessageDto
            {
                Id = bubbleId,
                Direction = "out",           // agent → customer
                Channel = "whatsapp",
                Text = bubbleText,
                SentAtUtc = sentAt,
                Status = status,
                ErrorMessage = errorMessage
            };

            return dto;
        }

        public async Task MarkConversationAsReadAsync(
            ChatInboxMarkReadRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(request));

            if (request.ContactId == Guid.Empty)
                throw new ArgumentException("ContactId is required.", nameof(request));

            if (request.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.", nameof(request));

            var businessId = request.BusinessId;
            var contactId = request.ContactId;
            var userId = request.UserId;
            var nowUtc = DateTime.UtcNow;

            var lastReadAt = request.LastReadAtUtc.HasValue
                ? (request.LastReadAtUtc.Value.Kind == DateTimeKind.Utc
                    ? request.LastReadAtUtc.Value
                    : request.LastReadAtUtc.Value.ToUniversalTime())
                : nowUtc;

            // Either insert or update ContactReads row.
            var existing = await _db.ContactReads
                .FirstOrDefaultAsync(
                    r => r.BusinessId == businessId
                         && r.ContactId == contactId
                         && r.UserId == userId,
                    ct)
                .ConfigureAwait(false);

            if (existing == null)
            {
                var entity = new ContactRead
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contactId,
                    UserId = userId,
                    LastReadAt = lastReadAt
                };

                await _db.ContactReads.AddAsync(entity, ct).ConfigureAwait(false);
            }
            else
            {
                // Only move forward in time; never move LastReadAt backwards.
                if (existing.LastReadAt < lastReadAt)
                {
                    existing.LastReadAt = lastReadAt;
                    _db.ContactReads.Update(existing);
                }
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        public async Task AssignConversationAsync(
            ChatInboxAssignRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(request));

            if (request.ContactId == Guid.Empty)
                throw new ArgumentException("ContactId is required.", nameof(request));

            if (request.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.", nameof(request));

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(
                    c => c.BusinessId == request.BusinessId && c.Id == request.ContactId,
                    ct)
                .ConfigureAwait(false);

            if (contact == null)
            {
                throw new InvalidOperationException("Contact not found for assignment.");
            }

            contact.AssignedAgentId = request.UserId;
            _db.Contacts.Update(contact);

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        public async Task UnassignConversationAsync(
            ChatInboxUnassignRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(request));

            if (request.ContactId == Guid.Empty)
                throw new ArgumentException("ContactId is required.", nameof(request));

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(
                    c => c.BusinessId == request.BusinessId && c.Id == request.ContactId,
                    ct)
                .ConfigureAwait(false);

            if (contact == null)
            {
                throw new InvalidOperationException("Contact not found for unassign.");
            }

            contact.AssignedAgentId = null;
            _db.Contacts.Update(contact);

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        public async Task ChangeConversationStatusAsync(
           ChatInboxChangeStatusRequestDto request,
           CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(request));

            if (request.ContactId == Guid.Empty)
                throw new ArgumentException("ContactId is required.", nameof(request));

            var rawStatus = request.TargetStatus ?? string.Empty;
            var normalized = rawStatus.Trim();

            if (string.IsNullOrEmpty(normalized))
                throw new ArgumentException("TargetStatus is required.", nameof(request));

            normalized = normalized.ToLowerInvariant();

            // We accept: "open", "closed", "new", "pending"
            var close = normalized switch
            {
                "closed" => true,
                "open" => false,
                "new" => false,
                "pending" => false,
                _ => throw new ArgumentException(
                    "TargetStatus must be one of: Open, Closed, New, Pending.",
                    nameof(request))
            };

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(
                    c => c.BusinessId == request.BusinessId && c.Id == request.ContactId,
                    ct)
                .ConfigureAwait(false);

            if (contact == null)
            {
                throw new InvalidOperationException("Contact not found for status change.");
            }

            if (close)
            {
                contact.IsArchived = true;
                contact.IsActive = false;
            }
            else
            {
                contact.IsArchived = false;
                contact.IsActive = true;
            }

            _db.Contacts.Update(contact);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}

