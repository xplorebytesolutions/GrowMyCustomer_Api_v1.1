// 📄 xbytechat-api/Features/ChatInbox/Services/ChatInboxQueryService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Models; // AppDbContext
// We avoid referencing MessageLog / Contact types by name so we don't fight namespaces.

namespace xbytechat.api.Features.ChatInbox.Services
{
    /// <summary>
    /// Default implementation of IChatInboxQueryService.
    /// 
    /// v1 implementation:
    ///  - Groups MessageLogs by ContactId for a Business.
    ///  - Joins Contacts for display name / phone.
    ///  - Computes last message, unread count (per user), 24h window, assignment flags.
    ///  - Applies tab filters ("live", "history", "unassigned", "my") and search.
    /// 
    /// This is intentionally conservative and can be optimized later
    /// (server-side aggregates, better indexes, source-type mapping, etc.).
    /// </summary>
    public sealed class ChatInboxQueryService : IChatInboxQueryService
    {
        private readonly AppDbContext _db;

        public ChatInboxQueryService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<ChatInboxConversationDto>> GetConversationsAsync(
            ChatInboxFilterDto filter,
            CancellationToken ct = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(filter));

            // Hard cap to avoid insane result sets
            var limit = filter.Limit <= 0 ? 50 : filter.Limit;
            if (limit > 200) limit = 200;

            var businessId = filter.BusinessId;
            var currentUserId = filter.CurrentUserId;

            // Base query: all message logs for this business that are linked to a contact.
            // NOTE: we rely on AppDbContext.MessageLogs and ContactId being non-null for chat contacts.
            var baseMessagesQuery = _db.MessageLogs
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.ContactId != null);

            // --- 1) Aggregate per contact: last message, first seen, total count ---
            // This is done server-side; we only bring down a small projection.
            var convoAggregates = await baseMessagesQuery
                .GroupBy(m => m.ContactId!.Value)
                .Select(g => new
                {
                    ContactId = g.Key,
                    LastMessageAt = g.Max(m => m.CreatedAt),
                    FirstSeenAt = g.Min(m => m.CreatedAt),
                    TotalMessages = g.Count()
                })
                .OrderByDescending(x => x.LastMessageAt)
                .Take(limit)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (convoAggregates.Count == 0)
            {
                return Array.Empty<ChatInboxConversationDto>();
            }

            var contactIds = convoAggregates.Select(x => x.ContactId).ToList();

            // --- 2) Load contacts for those ids (CRM) ---
            // We assume AppDbContext.Contacts exists and has basic fields we need.
            var contacts = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.BusinessId == businessId && contactIds.Contains(c.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var contactsById = contacts.ToDictionary(c => c.Id, c => c);

            // --- 3) Load last messages for preview (one per contact) ---
            // We re-query MessageLogs but only for the selected contactIds.
            var lastMessages = await _db.MessageLogs
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId
                            && m.ContactId != null
                            && contactIds.Contains(m.ContactId.Value))
                .GroupBy(m => m.ContactId!.Value)
                .Select(g => g
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault())
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var lastMessageByContactId = lastMessages
                .Where(m => m != null && m.ContactId != null)
                .ToDictionary(m => m!.ContactId!.Value, m => m!);

            // --- 4) Compute unread counts for the current user (if any) ---
            var unreadCounts = new Dictionary<Guid, int>();

            if (currentUserId.HasValue)
            {
                // ContactReads for this user + business
                var reads = await _db.ContactReads
                    .AsNoTracking()
                    .Where(r => r.BusinessId == businessId
                                && r.UserId == currentUserId.Value
                                && contactIds.Contains(r.ContactId))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var lastReadByContact = reads.ToDictionary(r => r.ContactId, r => r.LastReadAt);

                // Inbound messages for those contacts
                var inboundMessages = await _db.MessageLogs
                    .AsNoTracking()
                    .Where(m => m.BusinessId == businessId
                                && m.ContactId != null
                                && contactIds.Contains(m.ContactId.Value)
                                && m.IsIncoming)
                    .Select(m => new { m.ContactId, m.CreatedAt })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var group in inboundMessages.GroupBy(x => x.ContactId!.Value))
                {
                    var cid = group.Key;
                    DateTime? lastRead = null;
                    if (lastReadByContact.TryGetValue(cid, out var value))
                    {
                        lastRead = value;
                    }

                    var count = lastRead.HasValue
                        ? group.Count(x => x.CreatedAt > lastRead.Value)
                        : group.Count();

                    unreadCounts[cid] = count;
                }
            }

            // --- 5) Load session state for "mode" (automation vs agent), if available ---
            // We assume ChatSessionStates table exists and tracks Mode + last touch.
            var sessionStates = await _db.ChatSessionStates
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId && contactIds.Contains(s.ContactId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var sessionByContactId = sessionStates.ToDictionary(s => s.ContactId, s => s);

            // --- 6) Build DTOs in memory ---
            var nowUtc = DateTime.UtcNow;
            var results = new List<ChatInboxConversationDto>(convoAggregates.Count);

            foreach (var agg in convoAggregates)
            {
                if (!contactsById.TryGetValue(agg.ContactId, out var contact))
                {
                    // Contact might have been hard-deleted. Skip for now.
                    continue;
                }

                lastMessageByContactId.TryGetValue(agg.ContactId, out var lastMsg);

                var preview = lastMsg?.RenderedBody ?? lastMsg?.MessageContent ?? string.Empty;
                if (preview.Length > 140)
                {
                    preview = preview.Substring(0, 140) + "…";
                }

                var unread = unreadCounts.TryGetValue(agg.ContactId, out var count) ? count : 0;

                var within24h = (nowUtc - agg.LastMessageAt).TotalHours <= 24;

                // Conversation status heuristic:
                // - Archived / inactive contact => Closed
                // - Else if unread > 0 => Open
                // - Else => Pending
                var status =
                    (contact.IsArchived || !contact.IsActive) ? "Closed"
                    : unread > 0 ? "Open"
                    : "Pending";

                // Assignment
                var assignedUserId = contact.AssignedAgentId;
                var assignedUserIdString = assignedUserId?.ToString();
                var isAssignedToMe =
                    currentUserId.HasValue &&
                    assignedUserId.HasValue &&
                    assignedUserId.Value == currentUserId.Value;

                // Mode: if we have ChatSessionState, use that; else infer from last message.
                string mode = "automation";
                if (sessionByContactId.TryGetValue(agg.ContactId, out var session))
                {
                    // Assuming session.Mode is an enum or string; normalize to lower-case string.
                    mode = session.Mode?.ToString().ToLowerInvariant() ?? "automation";
                }
                else if (lastMsg != null)
                {
                    // Fallback: if last message is an outgoing "agent" message
                    // we treat it as agent mode; otherwise automation.
                    // (This depends on how you store Source; adjust later.)
                    if (!lastMsg.IsIncoming && string.Equals(lastMsg.Source, "agent", StringComparison.OrdinalIgnoreCase))
                    {
                        mode = "agent";
                    }
                }

                // For v1 we don't yet decode exact SourceType / SourceName from Campaign / AutoReply / CTAFlow.
                // We'll set "Unknown" and fill this later when we wire analytics.
                var dto = new ChatInboxConversationDto
                {
                    // For v1 we use the ContactId as conversation id.
                    Id = agg.ContactId.ToString(),

                    ContactId = agg.ContactId,
                    ContactName = string.IsNullOrWhiteSpace(contact.Name)
                        ? (contact.ProfileName ?? contact.PhoneNumber)
                        : contact.Name,
                    ContactPhone = contact.PhoneNumber,

                    LastMessagePreview = preview,
                    LastMessageAt = agg.LastMessageAt,

                    UnreadCount = unread,
                    Status = status,

                    // NumberId/NumberLabel: for now we leave empty.
                    // Once WhatsApp phone mapping is wired, we can fill these.
                    NumberId = string.Empty,
                    NumberLabel = string.Empty,

                    Within24h = within24h,

                    AssignedToUserId = assignedUserIdString,
                    AssignedToUserName = null,  // will be filled in v2 by joining Users table
                    IsAssignedToMe = isAssignedToMe,

                    Mode = mode,
                    SourceType = "Unknown",
                    SourceName = null,

                    FirstSeenAt = agg.FirstSeenAt,
                    TotalMessages = agg.TotalMessages,

                    LastAgentReplyAt = null,     // can be filled later via MessageLogs aggregate
                    LastAutomationAt = null      // same as above
                };

                results.Add(dto);
            }

            // --- 7) Apply tab filters ("live", "history", "unassigned", "my") + number + search ---

            IEnumerable<ChatInboxConversationDto> filtered = results;

            if (!string.IsNullOrWhiteSpace(filter.Tab))
            {
                var tab = filter.Tab.ToLowerInvariant();
                switch (tab)
                {
                    case "live":
                        filtered = filtered.Where(c => c.Within24h);
                        break;
                    case "history":
                        filtered = filtered.Where(c => !c.Within24h);
                        break;
                    // "unassigned" and "my" were already mapped to flags in the controller,
                    // but we double-check here too (harmless).
                    case "unassigned":
                        filtered = filtered.Where(c => string.IsNullOrEmpty(c.AssignedToUserId));
                        break;
                    case "my":
                        if (currentUserId.HasValue)
                        {
                            filtered = filtered.Where(c => c.IsAssignedToMe);
                        }
                        break;
                    default:
                        break;
                }
            }

            if (filter.OnlyUnassigned)
            {
                filtered = filtered.Where(c => string.IsNullOrEmpty(c.AssignedToUserId));
            }

            if (filter.OnlyAssignedToMe && currentUserId.HasValue)
            {
                filtered = filtered.Where(c => c.IsAssignedToMe);
            }

            // NumberId filter: for now we don't yet know which number a conversation belongs to.
            // Once MessageLogs have NumberId / PhoneNumberId we can populate dto.NumberId and filter here.
            if (!string.IsNullOrWhiteSpace(filter.NumberId)
                && !string.Equals(filter.NumberId, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => string.Equals(c.NumberId, filter.NumberId, StringComparison.OrdinalIgnoreCase));
            }

            // Search: name, phone, last message preview (case-insensitive)
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                var termLower = term.ToLowerInvariant();

                filtered = filtered.Where(c =>
                    (!string.IsNullOrEmpty(c.ContactName) && c.ContactName.ToLowerInvariant().Contains(termLower)) ||
                    (!string.IsNullOrEmpty(c.ContactPhone) && c.ContactPhone.Contains(term)) ||
                    (!string.IsNullOrEmpty(c.LastMessagePreview) && c.LastMessagePreview.ToLowerInvariant().Contains(termLower)));
            }

            // Final cap (defensive)
            var finalList = filtered
                .OrderByDescending(c => c.LastMessageAt)
                .Take(limit)
                .ToList();

            return finalList;
        }

        // 💬 Messages for a single conversation (center pane)
        // inside ChatInboxQueryService

        // 💬 Messages for a single conversation (center pane)
        // inside ChatInboxQueryService

        public async Task<IReadOnlyList<ChatInboxMessageDto>> GetMessagesForConversationAsync(
            Guid businessId,
            string contactPhone,
            int limit,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId must be a non-empty GUID.", nameof(businessId));

            if (string.IsNullOrWhiteSpace(contactPhone))
                throw new ArgumentException("Contact phone is required.", nameof(contactPhone));

            if (limit <= 0)
                limit = 50;
            if (limit > 500)
                limit = 500;

            var trimmedPhone = contactPhone.Trim();

            // 🟢 Step 1: resolve ContactId from phone number for this business
            var contactId = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.BusinessId == businessId && c.PhoneNumber == trimmedPhone)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (contactId == null)
            {
                // No such contact → no messages
                return Array.Empty<ChatInboxMessageDto>();
            }

            // 🟢 Step 2: fetch all messages for this contact (both directions)
            var query = _db.MessageLogs
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId &&
                            x.ContactId == contactId.Value);

            // Newest first
            query = query
                .OrderByDescending(x => x.SentAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(limit);

            var rows = await query
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Map to DTOs (newest → oldest; UI can reverse if needed).
            var list = rows
                .Select(x =>
                {
                    var instant = x.SentAt ?? x.CreatedAt;
                    var utcInstant = instant.Kind == DateTimeKind.Utc
                        ? instant
                        : instant.ToUniversalTime();

                    return new ChatInboxMessageDto
                    {
                        Id = x.Id,

                        // ✅ Use MessageLog.IsIncoming to decide bubble side
                        Direction = x.IsIncoming ? "in" : "out",

                        Channel = "whatsapp",

                        // Prefer rendered template body when available
                        Text = x.RenderedBody ?? x.MessageContent ?? string.Empty,

                        SentAtUtc = utcInstant,
                        Status = x.Status,
                        ErrorMessage = x.ErrorMessage
                    };
                })
                .ToList();

            return list;
        }


    }
}
