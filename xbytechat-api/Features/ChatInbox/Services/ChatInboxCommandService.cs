// 📄 xbytechat-api/Features/ChatInbox/Services/ChatInboxCommandService.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.AuthModule.Models;                 // User
using xbytechat.api.Features.AccessControl.Models;     // Permissions, RolePermissions, UserPermissions
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.CRM.Models;               // Contact
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Helpers;
using xbytechat.api.Models;                            // AppDbContext, MessageLog

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// SharedInInbox visibility + Assigned-only reply (enterprise).
    ///
    /// Rules (Shared visibility):
    /// 1) Anyone can SEE chats (SharedInInbox) — enforced on query layer/UI.
    /// 2) Reply allowed ONLY when:
    ///    - chat is assigned to actor, OR
    ///    - actor is Business/Platform privileged, OR
    ///    - actor has INBOX.CHAT.ASSIGN (manager-style override)
    /// 3) Unassigned chats:
    ///    - ✅ Any ACTIVE agent can "claim on reply" (auto self-assign) and reply.
    /// </summary>
    public sealed class ChatInboxCommandService : IChatInboxCommandService
    {
        private const string InboxAssignPermissionCode = "INBOX.CHAT.ASSIGN";

        private readonly AppDbContext _db;
        private readonly IMessageEngineService _messageEngine;

        public ChatInboxCommandService(AppDbContext db, IMessageEngineService messageEngine)
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

            // NOTE: ActorUserId must be set server-side from token (do NOT trust UI).
            if (request.ActorUserId == Guid.Empty)
                throw new ArgumentException("ActorUserId is required (server-side).", nameof(request));

            if (string.IsNullOrWhiteSpace(request.To))
                throw new ArgumentException("Target phone (To) is required.", nameof(request));

            var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();
            var mediaId = string.IsNullOrWhiteSpace(request.MediaId) ? null : request.MediaId.Trim();
            var mediaType = string.IsNullOrWhiteSpace(request.MediaType) ? null : request.MediaType.Trim().ToLowerInvariant();
            var hasLocation = request.LocationLatitude.HasValue && request.LocationLongitude.HasValue;

            var hasText = !string.IsNullOrWhiteSpace(text);
            var hasMedia = !string.IsNullOrWhiteSpace(mediaId);

            if (!hasText && !hasMedia && !hasLocation)
                throw new ArgumentException("Either Text, MediaId, or Location is required.", nameof(request));

            if (hasMedia && mediaType is not ("image" or "document" or "video" or "audio"))
                throw new ArgumentException("MediaType must be 'image', 'document', 'video', or 'audio'.", nameof(request));

            if (hasMedia && mediaType == "audio" && hasText)
                throw new ArgumentException("Audio messages do not support captions. Please remove Text.", nameof(request));

            if (hasLocation && (hasMedia || hasText))
                throw new ArgumentException("Location messages cannot include Text or MediaId.", nameof(request));

            if (hasLocation)
            {
                var lat = request.LocationLatitude!.Value;
                var lon = request.LocationLongitude!.Value;
                if (lat < -90 || lat > 90) throw new ArgumentException("LocationLatitude must be between -90 and 90.", nameof(request));
                if (lon < -180 || lon > 180) throw new ArgumentException("LocationLongitude must be between -180 and 180.", nameof(request));
            }

            var businessId = request.BusinessId;
            var actorUserId = request.ActorUserId;
            var phone = request.To.Trim();

            // ✅ Load actor (must be active and belong to business)
            var actor = await LoadActiveBusinessUserAsync(businessId, actorUserId, ct).ConfigureAwait(false);

            // ✅ Resolve & load contact (tracked)
            var contact = await LoadTrackedContactAsync(businessId, request.ContactId, phone, ct).ConfigureAwait(false);

            // ✅ Closed / archived / inactive => block
            EnsureConversationIsReplyable(contact);

            // ✅ Enforce reply rules (includes claim-on-reply for ANY agent)
            await EnforceAssignedOnlyReplyAsync(actor, contact, ct).ConfigureAwait(false);

            // 📨 Send via MessagesEngine
            var result =
                hasMedia
                    ? await SendMediaAsync(businessId, phone, contact.Id, request, mediaId!, mediaType!, text).ConfigureAwait(false)
                    : hasLocation
                        ? await SendLocationAsync(businessId, phone, contact.Id, request).ConfigureAwait(false)
                        : await _messageEngine.SendTextDirectAsync(new TextMessageSendDto
                        {
                            BusinessId = businessId,
                            RecipientNumber = phone,
                            TextContent = text!,
                            ContactId = contact.Id,
                            PhoneNumberId = string.IsNullOrWhiteSpace(request.NumberId) ? null : request.NumberId.Trim(),
                            Provider = null,
                            Source = "agent"
                        }).ConfigureAwait(false);

            // Load log for richer bubble
            MessageLog? log = null;
            if (result.LogId.HasValue)
            {
                log = await _db.MessageLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == result.LogId.Value, ct)
                    .ConfigureAwait(false);
            }

            // Update conversation meta (outbound)
            var nowUtc = DateTime.UtcNow;
            contact.LastOutboundAt = nowUtc;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            var bubbleId = log?.Id ?? Guid.NewGuid();
            var bubbleText = log?.MessageContent ?? (text ?? string.Empty);

            var ts = log?.SentAt ?? log?.CreatedAt ?? nowUtc;
            var sentAtUtc = ts.Kind == DateTimeKind.Utc ? ts : ts.ToUniversalTime();

            var status = log?.Status ?? (result.Success ? "Sent" : "Failed");
            var err = log?.ErrorMessage ?? (result.Success ? null : result.Message);

            return new ChatInboxMessageDto
            {
                Id = bubbleId,
                Direction = "out",
                Channel = "whatsapp",
                Text = bubbleText,
                MediaId = log?.MediaId,
                MediaType = log?.MediaType,
                FileName = log?.FileName,
                MimeType = log?.MimeType,
                LocationLatitude = log?.LocationLatitude,
                LocationLongitude = log?.LocationLongitude,
                LocationName = log?.LocationName,
                LocationAddress = log?.LocationAddress,
                SentAtUtc = sentAtUtc,
                Status = status,
                ErrorMessage = err
            };
        }

        private async Task<ResponseResult> SendMediaAsync(
            Guid businessId,
            string to,
            Guid contactId,
            ChatInboxSendMessageRequestDto request,
            string mediaId,
            string mediaType,
            string? caption)
        {
            var dto = new MediaMessageSendDto
            {
                BusinessId = businessId,
                RecipientNumber = to,
                MediaId = mediaId,
                Caption = caption,
                FileName = string.IsNullOrWhiteSpace(request.FileName) ? null : request.FileName.Trim(),
                MimeType = string.IsNullOrWhiteSpace(request.MimeType) ? null : request.MimeType.Trim(),
                ContactId = contactId,
                PhoneNumberId = string.IsNullOrWhiteSpace(request.NumberId) ? null : request.NumberId.Trim(),
                Provider = null,
                Source = "agent"
            };

            return mediaType switch
            {
                "image" => await _messageEngine.SendImageDirectAsync(dto).ConfigureAwait(false),
                "document" => await _messageEngine.SendDocumentDirectAsync(dto).ConfigureAwait(false),
                "video" => await _messageEngine.SendVideoDirectAsync(dto).ConfigureAwait(false),
                "audio" => await _messageEngine.SendAudioDirectAsync(dto).ConfigureAwait(false),
                _ => throw new ArgumentException("Unsupported MediaType.", nameof(mediaType))
            };
        }

        private Task<ResponseResult> SendLocationAsync(
            Guid businessId,
            string to,
            Guid contactId,
            ChatInboxSendMessageRequestDto request)
        {
            var dto = new LocationMessageSendDto
            {
                BusinessId = businessId,
                RecipientNumber = to,
                ContactId = contactId,
                PhoneNumberId = string.IsNullOrWhiteSpace(request.NumberId) ? null : request.NumberId.Trim(),
                Provider = null,
                Source = "agent",
                Latitude = request.LocationLatitude!.Value,
                Longitude = request.LocationLongitude!.Value,
                Name = string.IsNullOrWhiteSpace(request.LocationName) ? null : request.LocationName.Trim(),
                Address = string.IsNullOrWhiteSpace(request.LocationAddress) ? null : request.LocationAddress.Trim()
            };

            return _messageEngine.SendLocationDirectAsync(dto);
        }

        //public async Task MarkConversationAsReadAsync(ChatInboxMarkReadRequestDto request, CancellationToken ct = default)
        //{
        //    if (request == null) throw new ArgumentNullException(nameof(request));
        //    if (request.BusinessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(request));
        //    if (request.ContactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(request));
        //    if (request.UserId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(request));

        //    var businessId = request.BusinessId;
        //    var contactId = request.ContactId;
        //    var userId = request.UserId;
        //    var nowUtc = DateTime.UtcNow;

        //    var lastReadAt = request.LastReadAtUtc.HasValue
        //        ? (request.LastReadAtUtc.Value.Kind == DateTimeKind.Utc
        //            ? request.LastReadAtUtc.Value
        //            : request.LastReadAtUtc.Value.ToUniversalTime())
        //        : nowUtc;

        //    var existing = await _db.ContactReads
        //        .FirstOrDefaultAsync(
        //            r => r.BusinessId == businessId && r.ContactId == contactId && r.UserId == userId,
        //            ct)
        //        .ConfigureAwait(false);

        //    if (existing == null)
        //    {
        //        await _db.ContactReads.AddAsync(new ContactRead
        //        {
        //            Id = Guid.NewGuid(),
        //            BusinessId = businessId,
        //            ContactId = contactId,
        //            UserId = userId,
        //            LastReadAt = lastReadAt
        //        }, ct).ConfigureAwait(false);
        //    }
        //    else
        //    {
        //        // Only move forward in time; never go backwards.
        //        if (existing.LastReadAt < lastReadAt)
        //        {
        //            existing.LastReadAt = lastReadAt;
        //            _db.ContactReads.Update(existing);
        //        }
        //    }

        //    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        //}
        public async Task MarkConversationAsReadAsync(
      Guid businessId,
      Guid contactId,
      Guid userId,
      DateTime? lastReadAtUtc,
      CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));
            if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

            var nowUtc = DateTime.UtcNow;

            var lastReadAt = lastReadAtUtc.HasValue
                ? (lastReadAtUtc.Value.Kind == DateTimeKind.Utc
                    ? lastReadAtUtc.Value
                    : lastReadAtUtc.Value.ToUniversalTime())
                : nowUtc;

            var existing = await _db.ContactReads
                .FirstOrDefaultAsync(
                    r => r.BusinessId == businessId && r.ContactId == contactId && r.UserId == userId,
                    ct)
                .ConfigureAwait(false);

            if (existing == null)
            {
                await _db.ContactReads.AddAsync(new ContactRead
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contactId,
                    UserId = userId,
                    LastReadAt = lastReadAt
                }, ct).ConfigureAwait(false);
            }
            else
            {
                // Only move forward in time; never go backwards.
                if (existing.LastReadAt < lastReadAt)
                {
                    existing.LastReadAt = lastReadAt;
                }
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // -------------------------
        // Helpers
        // -------------------------

        private static void EnsureConversationIsReplyable(Contact contact)
        {
            var inboxStatus = (contact.InboxStatus ?? string.Empty).Trim();

            if (string.Equals(inboxStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                contact.IsArchived ||
                !contact.IsActive)
            {
                throw new InvalidOperationException("Conversation is closed.");
            }
        }

        private async Task<Contact> LoadTrackedContactAsync(Guid businessId, Guid? contactId, string phone, CancellationToken ct)
        {
            if (contactId.HasValue && contactId.Value != Guid.Empty)
            {
                var byId = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId.Value, ct)
                    .ConfigureAwait(false);

                if (byId != null) return byId;
            }

            var byPhone = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == phone, ct)
                .ConfigureAwait(false);

            if (byPhone == null)
                throw new InvalidOperationException("Contact not found for this conversation.");

            return byPhone;
        }

        private async Task<User> LoadActiveBusinessUserAsync(Guid businessId, Guid userId, CancellationToken ct)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                .ConfigureAwait(false);

            if (user == null) throw new InvalidOperationException("User not found.");
            if (user.BusinessId != businessId) throw new UnauthorizedAccessException("User does not belong to this business.");
            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("User is not active.");

            return user;
        }

        private async Task EnforceAssignedOnlyReplyAsync(User actor, Contact contact, CancellationToken ct)
        {
            // ✅ If already assigned to actor -> OK
            if (contact.AssignedAgentId.HasValue && contact.AssignedAgentId.Value == actor.Id)
                return;

            // ✅ Shared visibility rule: unassigned chat -> claim-on-first-reply (ANY active agent)
            if (!contact.AssignedAgentId.HasValue)
            {
                // Atomic claim to avoid race: two agents reply at same time
                var claimed = await _db.Contacts
                    .Where(c => c.BusinessId == contact.BusinessId
                                && c.Id == contact.Id
                                && c.AssignedAgentId == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.AssignedAgentId, actor.Id), ct)
                    .ConfigureAwait(false);

                if (claimed > 0)
                {
                    // Keep in-memory entity consistent
                    contact.AssignedAgentId = actor.Id;
                    return;
                }

                // Someone else claimed between read and write → reload for correct enforcement
                var fresh = await _db.Contacts
                    .AsNoTracking()
                    .Where(c => c.BusinessId == contact.BusinessId && c.Id == contact.Id)
                    .Select(c => c.AssignedAgentId)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                contact.AssignedAgentId = fresh;
                // Now fall through to "assigned to someone else" logic below
            }

            // ✅ Assigned to someone else -> only Business owner OR INBOX.CHAT.ASSIGN can override
            var isBusinessOwner = IsBusinessOwner(actor);
            var canAssign = await HasPermissionAsync(actor.Id, InboxAssignPermissionCode, ct).ConfigureAwait(false);

            if (isBusinessOwner || canAssign)
                return;

            throw new UnauthorizedAccessException("Not allowed to reply. This chat is assigned to another agent.");
        }

        private static bool IsBusinessOwner(User actor)
        {
            var role = (actor.Role?.Name ?? string.Empty).Trim().ToLowerInvariant();
            return role == "business";
        }

        /// <summary>
        /// Privileged = Business owner or platform roles.
        /// Manager is NOT privileged unless they have INBOX.CHAT.ASSIGN.
        /// </summary>
        private static bool IsBusinessOrPlatformPrivileged(User actor)
        {
            var role = (actor.Role?.Name ?? string.Empty).Trim().ToLowerInvariant();
            return role is "business" or "admin" or "superadmin" or "partner";
        }

        private async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct)
        {
            var code = (permissionCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0) return false;

            var permissionId = await _db.Permissions
                .AsNoTracking()
                .Where(p => p.Code != null && p.Code.ToUpper() == code)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!permissionId.HasValue) return false;

            // Direct user permission
            var direct = await _db.UserPermissions
                .AsNoTracking()
                .AnyAsync(up =>
                    up.UserId == userId &&
                    up.PermissionId == permissionId.Value &&
                    up.IsGranted &&
                    !up.IsRevoked, ct)
                .ConfigureAwait(false);

            if (direct) return true;

            // Role permission
            var roleId = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.RoleId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!roleId.HasValue) return false;

            var byRole = await _db.RolePermissions
                .AsNoTracking()
                .AnyAsync(rp =>
                    rp.RoleId == roleId.Value &&
                    rp.PermissionId == permissionId.Value &&
                    rp.IsActive &&
                    !rp.IsRevoked, ct)
                .ConfigureAwait(false);

            return byRole;
        }
    }
}
