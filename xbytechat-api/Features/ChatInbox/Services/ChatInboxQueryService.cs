// 📄 xbytechat-api/Features/ChatInbox/Services/ChatInboxQueryService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Models;
using xbytechat.api.Features.ChatInbox.Utils;
using xbytechat.api.Models;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public sealed class ChatInboxQueryService : IChatInboxQueryService
    {
        private const string InboxAssignPermissionCode = "INBOX.CHAT.ASSIGN";

        private readonly AppDbContext _db;

        private sealed class ConversationsCursor
        {
            public DateTime LastMessageAtUtc { get; set; }
            public Guid ContactId { get; set; }
        }

        private sealed class MessagesCursor
        {
            public DateTime InstantUtc { get; set; }
            public Guid MessageId { get; set; }
        }

        public ChatInboxQueryService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ✅ Backward compatible (old behavior)
        public async Task<IReadOnlyList<ChatInboxConversationDto>> GetConversationsAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default)
        {
            var page = await GetConversationsPageAsync(filter, ct).ConfigureAwait(false);
            return page.Items;
        }

        // ✅ New: cursor page
        public async Task<PagedResultDto<ChatInboxConversationDto>> GetConversationsPageAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(filter));

            var limit = filter.Limit <= 0 ? 50 : filter.Limit;
            if (limit > 200) limit = 200;

            var businessId = filter.BusinessId;
            var currentUserId = filter.CurrentUserId;

            var cursorObj = CursorCodec.Decode<ConversationsCursor>(filter.Cursor);
            var within24hCutoffUtc = DateTime.UtcNow.AddHours(-24);

            var convoAgg =
                from m in _db.MessageLogs.AsNoTracking()
                where m.BusinessId == businessId && m.ContactId != null
                group m by m.ContactId!.Value
                into g
                select new
                {
                    ContactId = g.Key,
                    LastMessageAt = g.Max(x => x.SentAt ?? x.CreatedAt),
                    FirstSeenAt = g.Min(x => x.SentAt ?? x.CreatedAt),
                    TotalMessages = g.Count(),
                    LastInboundAt = g.Where(x => x.IsIncoming)
                        .Max(x => (DateTime?)(x.SentAt ?? x.CreatedAt)),
                    LastOutboundAt = g.Where(x => !x.IsIncoming)
                        .Max(x => (DateTime?)(x.SentAt ?? x.CreatedAt))
                };

            var q =
                from a in convoAgg
                join c in _db.Contacts.AsNoTracking()
                    on a.ContactId equals c.Id
                where c.BusinessId == businessId
                select new
                {
                    a.ContactId,
                    a.LastMessageAt,
                    a.FirstSeenAt,
                    a.TotalMessages,
                    a.LastInboundAt,
                    a.LastOutboundAt,
                    Contact = c
                };

            // ✅ Visibility mode (Shared vs Restricted)
            var visibility = await _db.Businesses
                .AsNoTracking()
                .Where(b => b.Id == businessId)
                .Select(b => (InboxVisibilityMode?)b.InboxVisibilityMode)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false) ?? InboxVisibilityMode.SharedInInbox;

            // ✅ Restricted mode: non-privileged agents only see chats assigned to them
            if (visibility == InboxVisibilityMode.AssignedOnly)
            {
                if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
                {
                    return new PagedResultDto<ChatInboxConversationDto>
                    {
                        Items = Array.Empty<ChatInboxConversationDto>(),
                        HasMore = false,
                        NextCursor = null
                    };
                }

                var canSeeAll = await CanSeeAllInRestrictedModeAsync(businessId, currentUserId.Value, ct)
                    .ConfigureAwait(false);

                if (!canSeeAll)
                {
                    q = q.Where(x => x.Contact.AssignedAgentId == currentUserId.Value);
                }
            }

            if (filter.OnlyUnassigned)
                q = q.Where(x => x.Contact.AssignedAgentId == null);

            if (filter.OnlyAssignedToMe && currentUserId.HasValue)
                q = q.Where(x => x.Contact.AssignedAgentId == currentUserId.Value);

            if (filter.ContactId.HasValue && filter.ContactId.Value != Guid.Empty)
                q = q.Where(x => x.ContactId == filter.ContactId.Value);

            var tab = (filter.Tab ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(tab))
            {
                if (tab == "closed")
                {
                    q = q.Where(x => x.Contact.InboxStatus == "Closed" || x.Contact.IsArchived || !x.Contact.IsActive);
                }
                else
                {
                    q = q.Where(x => x.Contact.InboxStatus != "Closed" && !x.Contact.IsArchived && x.Contact.IsActive);

                    if (tab == "live")
                    {
                        q = q.Where(x => x.LastInboundAt.HasValue && x.LastInboundAt.Value >= within24hCutoffUtc);
                    }
                    else if (tab is "older" or "history")
                    {
                        q = q.Where(x => !x.LastInboundAt.HasValue || x.LastInboundAt.Value < within24hCutoffUtc);
                    }
                    else if (tab == "unassigned")
                    {
                        q = q.Where(x => x.Contact.AssignedAgentId == null);
                    }
                    else if (tab == "my")
                    {
                        if (currentUserId.HasValue)
                            q = q.Where(x => x.Contact.AssignedAgentId == currentUserId.Value);
                        else
                            q = q.Where(x => false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var raw = filter.SearchTerm.Trim();
                var term = raw.ToLowerInvariant();

                q = q.Where(x =>
                    (!string.IsNullOrEmpty(x.Contact.Name) && x.Contact.Name.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(x.Contact.ProfileName) && x.Contact.ProfileName.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(x.Contact.PhoneNumber) && x.Contact.PhoneNumber.Contains(raw)));
            }

            if (cursorObj != null && cursorObj.ContactId != Guid.Empty)
            {
                var lm = DateTime.SpecifyKind(cursorObj.LastMessageAtUtc, DateTimeKind.Utc);
                var cid = cursorObj.ContactId;

                q = q.Where(x =>
                    x.LastMessageAt < lm ||
                    (x.LastMessageAt == lm && x.ContactId.CompareTo(cid) < 0));
            }

            q = q.OrderByDescending(x => x.LastMessageAt)
                 .ThenByDescending(x => x.ContactId);

            var rows = await q.Take(limit + 1).ToListAsync(ct).ConfigureAwait(false);

            var hasMore = rows.Count > limit;
            var pageRows = rows.Take(limit).ToList();

            if (pageRows.Count == 0)
            {
                return new PagedResultDto<ChatInboxConversationDto>
                {
                    Items = Array.Empty<ChatInboxConversationDto>(),
                    HasMore = false,
                    NextCursor = null
                };
            }

            var contactIds = pageRows.Select(x => x.ContactId).ToList();

            var lastMessages = await _db.MessageLogs
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId
                            && m.ContactId != null
                            && contactIds.Contains(m.ContactId.Value))
                .GroupBy(m => m.ContactId!.Value)
                .Select(g => g
                    .OrderByDescending(m => m.SentAt ?? m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .FirstOrDefault())
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var lastMessageByContactId = lastMessages
                .Where(m => m != null && m.ContactId != null)
                .ToDictionary(m => m!.ContactId!.Value, m => m!);

            var assignedUserIds = pageRows
                .Select(x => x.Contact.AssignedAgentId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var assignedUsersById = new Dictionary<Guid, string>();
            if (assignedUserIds.Count > 0)
            {
                var users = await _db.Users
                    .AsNoTracking()
                    .Where(u => assignedUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.Name ?? u.Email) })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                assignedUsersById = users.ToDictionary(x => x.Id, x => x.Name ?? "Unknown");
            }

            var unreadCounts = new Dictionary<Guid, int>();
            if (currentUserId.HasValue)
            {
                var uid = currentUserId.Value;

                var readsQuery = _db.ContactReads
                    .AsNoTracking()
                    .Where(r => r.BusinessId == businessId && r.UserId == uid);

                var unreadRows = await _db.MessageLogs
                    .AsNoTracking()
                    .Where(m => m.BusinessId == businessId
                                && m.ContactId != null
                                && contactIds.Contains(m.ContactId.Value)
                                && m.IsIncoming)
                    .GroupJoin(
                        readsQuery,
                        m => m.ContactId!.Value,
                        r => r.ContactId,
                        (m, reads) => new
                        {
                            ContactId = m.ContactId!.Value,
                            Instant = (m.SentAt ?? m.CreatedAt),
                            LastReadAt = reads.Select(x => (DateTime?)x.LastReadAt).FirstOrDefault()
                        })
                    .Where(x => !x.LastReadAt.HasValue || x.Instant > x.LastReadAt.Value)
                    .GroupBy(x => x.ContactId)
                    .Select(g => new { ContactId = g.Key, Count = g.Count() })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                unreadCounts = unreadRows.ToDictionary(x => x.ContactId, x => x.Count);
            }

            var sessionStates = await _db.ChatSessionStates
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId && contactIds.Contains(s.ContactId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var sessionByContactId = sessionStates.ToDictionary(s => s.ContactId, s => s);

            var items = new List<ChatInboxConversationDto>(pageRows.Count);

            foreach (var row in pageRows)
            {
                var contact = row.Contact;

                lastMessageByContactId.TryGetValue(row.ContactId, out var lastMsg);

                var preview = lastMsg?.RenderedBody ?? lastMsg?.MessageContent ?? string.Empty;
                if (string.IsNullOrWhiteSpace(preview) && lastMsg != null && !string.IsNullOrWhiteSpace(lastMsg.MediaType))
                {
                    var mt = lastMsg.MediaType.Trim().ToLowerInvariant();
                    var name = lastMsg.FileName ?? string.Empty;

                    if (mt == "image")
                        preview = "Photo";
                    else if (mt == "document")
                        preview = string.IsNullOrWhiteSpace(name) ? "PDF" : name;
                    else if (mt == "video")
                        preview = string.IsNullOrWhiteSpace(name) ? "Video" : name;
                    else if (mt == "audio")
                        preview = string.IsNullOrWhiteSpace(name) ? "Audio" : name;
                    else if (mt == "location")
                        preview = string.IsNullOrWhiteSpace(lastMsg.LocationName) ? "Location" : lastMsg.LocationName!;
                }
                if (preview.Length > 140) preview = preview.Substring(0, 140) + "…";

                var unread = unreadCounts.TryGetValue(row.ContactId, out var uc) ? uc : 0;

                var lastInboundAt = row.LastInboundAt ?? contact.LastInboundAt;
                var lastOutboundAt = row.LastOutboundAt ?? contact.LastOutboundAt;

                var within24h =
                    lastInboundAt.HasValue && lastInboundAt.Value >= within24hCutoffUtc;

                var statusRaw = (contact.InboxStatus ?? string.Empty).Trim();
                var statusLower = statusRaw.ToLowerInvariant();
                var status =
                    statusLower switch
                    {
                        "open" => "Open",
                        "pending" => "Pending",
                        "closed" => "Closed",
                        _ => (contact.IsArchived || !contact.IsActive) ? "Closed" : "Open"
                    };

                var assignedUserId = contact.AssignedAgentId;
                var assignedUserIdString = assignedUserId?.ToString();

                var isAssignedToMe =
                    currentUserId.HasValue &&
                    assignedUserId.HasValue &&
                    assignedUserId.Value == currentUserId.Value;

                string? assignedUserName = null;
                if (assignedUserId.HasValue && assignedUsersById.TryGetValue(assignedUserId.Value, out var nm))
                    assignedUserName = nm;

                var mode = "automation";
                if (sessionByContactId.TryGetValue(row.ContactId, out var session))
                {
                    mode = session.Mode?.ToString().ToLowerInvariant() ?? "automation";
                }
                else if (lastMsg != null)
                {
                    if (!lastMsg.IsIncoming &&
                        string.Equals(lastMsg.Source, "agent", StringComparison.OrdinalIgnoreCase))
                    {
                        mode = "agent";
                    }
                }

                items.Add(new ChatInboxConversationDto
                {
                    Id = row.ContactId.ToString(),
                    ContactId = row.ContactId,
                    ContactName = string.IsNullOrWhiteSpace(contact.Name)
                        ? (contact.ProfileName ?? contact.PhoneNumber)
                        : contact.Name,
                    ContactPhone = contact.PhoneNumber,

                    LastMessagePreview = preview,
                    LastMessageAt = row.LastMessageAt,

                    UnreadCount = unread,
                    Status = status,

                    NumberId = string.Empty,
                    NumberLabel = string.Empty,

                    Within24h = within24h,

                    AssignedToUserId = assignedUserIdString,
                    AssignedToUserName = assignedUserName,
                    IsAssignedToMe = isAssignedToMe,

                    Mode = mode,
                    SourceType = "Unknown",
                    SourceName = null,

                    FirstSeenAt = row.FirstSeenAt,
                    LastInboundAt = lastInboundAt,
                    LastOutboundAt = lastOutboundAt,
                    TotalMessages = row.TotalMessages,

                    LastAgentReplyAt = null,
                    LastAutomationAt = null
                });
            }

            items.Sort((a, b) =>
            {
                var aUnread = a.UnreadCount > 0;
                var bUnread = b.UnreadCount > 0;

                if (aUnread && !bUnread) return -1;
                if (!aUnread && bUnread) return 1;

                return b.LastMessageAt.CompareTo(a.LastMessageAt);
            });

            string? nextCursor = null;
            if (hasMore && items.Count > 0)
            {
                var last = pageRows.Last();
                nextCursor = CursorCodec.Encode(new ConversationsCursor
                {
                    LastMessageAtUtc = DateTime.SpecifyKind(last.LastMessageAt, DateTimeKind.Utc),
                    ContactId = last.ContactId
                });
            }

            return new PagedResultDto<ChatInboxConversationDto>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        // =============================================================
        // ✅ SECURED MESSAGE METHODS (use these from controllers)
        // =============================================================

        public async Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            Guid currentUserId,
            CancellationToken ct = default)
        {
            var page = await GetMessagesPageForConversationByPhoneAsync(businessId, contactPhone, limit, null, currentUserId, ct)
                .ConfigureAwait(false);

            return page.Items;
        }

        public async Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            Guid currentUserId,
            CancellationToken ct = default)
        {
            var page = await GetMessagesPageForConversationByContactIdAsync(businessId, contactId, limit, null, currentUserId, ct)
                .ConfigureAwait(false);

            return page.Items;
        }

        public Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByPhoneAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            string? cursor,
            Guid currentUserId,
            CancellationToken ct = default)
        {
            // Resolve contactId then delegate
            return GetMessagesPageForConversationByPhoneInternalAsync(businessId, contactPhone, limit, cursor, currentUserId, ct);
        }

        public Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            string? cursor,
            Guid currentUserId,
            CancellationToken ct = default)
        {
            return GetMessagesPageForConversationByContactIdInternalAsync(businessId, contactId, limit, cursor, currentUserId, ct);
        }

        // =============================================================
        // ⚠️ LEGACY MESSAGE METHODS (keep for compatibility only)
        // These DO NOT enforce restricted mode. Prefer secured overloads.
        // =============================================================

        public async Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            CancellationToken ct = default)
        {
            var page = await GetMessagesPageForConversationByPhoneAsync(businessId, contactPhone, limit, null, ct)
                .ConfigureAwait(false);

            return page.Items;
        }

        public async Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            CancellationToken ct = default)
        {
            var page = await GetMessagesPageForConversationByContactIdAsync(businessId, contactId, limit, null, ct)
                .ConfigureAwait(false);

            return page.Items;
        }

        public Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByPhoneAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            string? cursor,
            CancellationToken ct = default)
        {
            // Legacy behavior: no visibility enforcement (Shared-like)
            return GetMessagesPageForConversationByPhoneInternalAsync(businessId, contactPhone, limit, cursor, null, ct);
        }

        public Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByContactIdAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            string? cursor,
            CancellationToken ct = default)
        {
            // Legacy behavior: no visibility enforcement (Shared-like)
            return GetMessagesPageForConversationByContactIdInternalAsync(businessId, contactId, limit, cursor, null, ct);
        }

        // =============================================================
        // Internal implementations with optional enforcement
        // =============================================================

        private async Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByPhoneInternalAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            string? cursor,
            Guid? currentUserId,
            CancellationToken ct)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId must be a non-empty GUID.", nameof(businessId));

            if (string.IsNullOrWhiteSpace(contactPhone))
                throw new ArgumentException("Contact phone is required.", nameof(contactPhone));

            var trimmedPhone = contactPhone.Trim();

            var contactId = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.BusinessId == businessId && c.PhoneNumber == trimmedPhone)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!contactId.HasValue)
            {
                return new PagedResultDto<ChatInboxMessageDto>
                {
                    Items = Array.Empty<ChatInboxMessageDto>(),
                    HasMore = false,
                    NextCursor = null
                };
            }

            return await GetMessagesPageForConversationByContactIdInternalAsync(
                    businessId, contactId.Value, limit, cursor, currentUserId, ct)
                .ConfigureAwait(false);
        }

        private async Task<PagedResultDto<ChatInboxMessageDto>> GetMessagesPageForConversationByContactIdInternalAsync(
            Guid businessId,
            Guid contactId,
            int limit,
            string? cursor,
            Guid? currentUserId,
            CancellationToken ct)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId must be a non-empty GUID.", nameof(businessId));

            if (contactId == Guid.Empty)
                throw new ArgumentException("ContactId must be a non-empty GUID.", nameof(contactId));

            if (limit <= 0) limit = 50;
            if (limit > 200) limit = 200;

            // ✅ Visibility enforcement for messages in Restricted mode
            await EnsureCanViewConversationAsync(businessId, contactId, currentUserId, ct)
                .ConfigureAwait(false);

            var cursorObj = CursorCodec.Decode<MessagesCursor>(cursor);

            var q = _db.MessageLogs
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.ContactId == contactId);

            if (cursorObj != null && cursorObj.MessageId != Guid.Empty)
            {
                var inst = DateTime.SpecifyKind(cursorObj.InstantUtc, DateTimeKind.Utc);
                var mid = cursorObj.MessageId;

                q = q.Where(x =>
                    (x.SentAt ?? x.CreatedAt) < inst ||
                    ((x.SentAt ?? x.CreatedAt) == inst && x.Id.CompareTo(mid) < 0));
            }

            var rows = await q
                .OrderByDescending(x => x.SentAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var hasMore = rows.Count > limit;
            var pageRows = rows.Take(limit).ToList();

            var items = pageRows.Select(x =>
            {
                var instant = x.SentAt ?? x.CreatedAt;
                var utcInstant = instant.Kind == DateTimeKind.Utc ? instant : instant.ToUniversalTime();

                return new ChatInboxMessageDto
                {
                    Id = x.Id,
                    Direction = x.IsIncoming ? "in" : "out",
                    Channel = "whatsapp",
                    Text = x.RenderedBody ?? x.MessageContent ?? string.Empty,
                    MediaId = x.MediaId,
                    MediaType = x.MediaType,
                    FileName = x.FileName,
                    MimeType = x.MimeType,
                    LocationLatitude = x.LocationLatitude,
                    LocationLongitude = x.LocationLongitude,
                    LocationName = x.LocationName,
                    LocationAddress = x.LocationAddress,
                    SentAtUtc = utcInstant,
                    Status = x.Status,
                    ErrorMessage = x.ErrorMessage
                };
            }).ToList();

            string? nextCursor = null;
            if (hasMore && pageRows.Count > 0)
            {
                var last = pageRows.Last();
                var instant = last.SentAt ?? last.CreatedAt;
                var utcInstant = instant.Kind == DateTimeKind.Utc ? instant : instant.ToUniversalTime();

                nextCursor = CursorCodec.Encode(new MessagesCursor
                {
                    InstantUtc = utcInstant,
                    MessageId = last.Id
                });
            }

            return new PagedResultDto<ChatInboxMessageDto>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        private async Task EnsureCanViewConversationAsync(
            Guid businessId,
            Guid contactId,
            Guid? currentUserId,
            CancellationToken ct)
        {
            // Shared mode => everyone in business can view.
            var visibility = await _db.Businesses
                .AsNoTracking()
                .Where(b => b.Id == businessId)
                .Select(b => (InboxVisibilityMode?)b.InboxVisibilityMode)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false) ?? InboxVisibilityMode.SharedInInbox;

            if (visibility != InboxVisibilityMode.AssignedOnly)
                return;

            if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("Restricted inbox: user context required.");

            var canSeeAll = await CanSeeAllInRestrictedModeAsync(businessId, currentUserId.Value, ct)
                .ConfigureAwait(false);

            if (canSeeAll)
                return;

            // ✅ Must be assigned to this user
            var allowed = await _db.Contacts
                .AsNoTracking()
                .AnyAsync(c =>
                    c.BusinessId == businessId &&
                    c.Id == contactId &&
                    c.AssignedAgentId == currentUserId.Value, ct)
                .ConfigureAwait(false);

            if (!allowed)
                throw new UnauthorizedAccessException("Restricted inbox: you are not assigned to this conversation.");
        }

        // ===========================
        // Restricted-mode helpers
        // ===========================

        private static bool IsPrivilegedRoleName(string? roleName)
        {
            var role = (roleName ?? string.Empty).Trim().ToLowerInvariant();
            return role is "admin" or "business" or "superadmin" or "partner";
        }

        private async Task<bool> CanSeeAllInRestrictedModeAsync(Guid businessId, Guid userId, CancellationToken ct)
        {
            var userRow = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && u.BusinessId == businessId && !u.IsDeleted && u.Status == "Active")
                .Select(u => new
                {
                    u.RoleId,
                    RoleName = u.Role != null ? u.Role.Name : null
                })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (userRow == null) return false;

            if (IsPrivilegedRoleName(userRow.RoleName))
                return true;

            return await HasPermissionAsync(userId, InboxAssignPermissionCode, ct).ConfigureAwait(false);
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

            var direct = await _db.UserPermissions
                .AsNoTracking()
                .AnyAsync(up =>
                    up.UserId == userId &&
                    up.PermissionId == permissionId.Value &&
                    up.IsGranted &&
                    !up.IsRevoked, ct)
                .ConfigureAwait(false);

            if (direct) return true;

            var roleId = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.RoleId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!roleId.HasValue) return false;

            return await _db.RolePermissions
                .AsNoTracking()
                .AnyAsync(rp =>
                    rp.RoleId == roleId.Value &&
                    rp.PermissionId == permissionId.Value &&
                    rp.IsActive &&
                    !rp.IsRevoked, ct)
                .ConfigureAwait(false);
        }
    }
}
