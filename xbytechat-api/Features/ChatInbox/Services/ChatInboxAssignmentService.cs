using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Inbox.Hubs;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public sealed class ChatInboxAssignmentService : IChatInboxAssignmentService
    {
        private const string InboxAssignPermissionCode = "INBOX.CHAT.ASSIGN";

        private readonly AppDbContext _db;
        private readonly ILogger<ChatInboxAssignmentService> _logger;
        private readonly IHubContext<InboxHub> _hub;

        public ChatInboxAssignmentService(
            AppDbContext db,
            ILogger<ChatInboxAssignmentService> logger,
            IHubContext<InboxHub> hub)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        public async Task<List<AgentDto>> GetAgentsAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(businessId));

            var agents = await _db.Users
                .AsNoTracking()
                .Where(u => u.BusinessId == businessId && !u.IsDeleted && u.Status == "Active")
                .Include(u => u.Role)
                .OrderBy(u => u.Name)
                .Select(u => new AgentDto
                {
                    Id = u.Id,
                    Name = u.Name ?? u.Email,
                    Email = u.Email,
                    RoleName = u.Role != null ? u.Role.Name : null
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return agents;
        }

        public async Task AssignAsync(
            Guid businessId,
            Guid contactId,
            Guid userId,
            Guid actorUserId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));
            if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
            if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            var actor = await LoadActiveBusinessUserAsync(businessId, actorUserId, ct).ConfigureAwait(false);
            _ = await LoadActiveBusinessUserAsync(businessId, userId, ct).ConfigureAwait(false); // validate target exists + active

            var isSelfAssign = actorUserId == userId;
            var canAssignOthers = await CanAssignOthersAsync(actor, ct).ConfigureAwait(false);

            // ? If assigning someone else, permission required
            if (!isSelfAssign && !canAssignOthers)
                throw new UnauthorizedAccessException("Not allowed to assign conversations to other agents.");

            // ? Atomic rules:
            // - Privileged/assign-perm can assign/reassign freely.
            // - Self-assign without privilege is allowed ONLY when currently unassigned (prevents stealing).
            var q = _db.Contacts.Where(c => c.BusinessId == businessId && c.Id == contactId);

            if (isSelfAssign && !canAssignOthers)
                q = q.Where(c => c.AssignedAgentId == null);

            var updated = await q
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.AssignedAgentId, userId),
                    ct)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                if (isSelfAssign && !canAssignOthers)
                    throw new UnauthorizedAccessException("Not allowed to self-assign. This chat is already assigned to another agent.");

                throw new InvalidOperationException("Contact not found for assignment.");
            }

            _logger.LogInformation(
                "ChatInbox assigned. BusinessId={BusinessId} ContactId={ContactId} AssignedToUserId={AssignedToUserId} ActorUserId={ActorUserId}",
                businessId, contactId, userId, actorUserId);

            await BroadcastRefreshAsync(businessId).ConfigureAwait(false);
        }



        public async Task UnassignAsync(Guid businessId, Guid contactId, Guid actorUserId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));
            if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            var actor = await LoadActiveBusinessUserAsync(businessId, actorUserId, ct).ConfigureAwait(false);
            var canAssignOthers = await CanAssignOthersAsync(actor, ct).ConfigureAwait(false);

            // ? Atomic rules:
            // - Privileged/assign-perm can unassign freely.
            // - Non-privileged can unassign ONLY if currently assigned to them (or already unassigned).
            var q = _db.Contacts.Where(c => c.BusinessId == businessId && c.Id == contactId);

            if (!canAssignOthers)
                q = q.Where(c => c.AssignedAgentId == null || c.AssignedAgentId == actorUserId);

            var updated = await q
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.AssignedAgentId, (Guid?)null), ct)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                // ? Perfect distinction: not found vs not allowed
                var exists = await _db.Contacts
                    .AsNoTracking()
                    .AnyAsync(c => c.BusinessId == businessId && c.Id == contactId, ct)
                    .ConfigureAwait(false);

                if (!exists)
                    throw new InvalidOperationException("Contact not found.");

                throw new UnauthorizedAccessException("Not allowed to unassign conversations owned by another agent.");
            }

            _logger.LogInformation(
                "ChatInbox unassigned. BusinessId={BusinessId} ContactId={ContactId} ActorUserId={ActorUserId}",
                businessId, contactId, actorUserId);

            await BroadcastRefreshAsync(businessId).ConfigureAwait(false);
        }


        public async Task SetStatusAsync(
            Guid businessId,
            Guid contactId,
            string status,
            Guid actorUserId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new ArgumentException("BusinessId is required.", nameof(businessId));
            if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));
            if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

            var normalized = NormalizeStatus(status);
            if (normalized == null)
                throw new ArgumentException("Status must be one of: Open, Pending, Closed.", nameof(status));

            var actor = await LoadActiveBusinessUserAsync(businessId, actorUserId, ct).ConfigureAwait(false);
            var contact = await LoadBusinessContactAsync(businessId, contactId, ct).ConfigureAwait(false);

            var canUpdate =
                IsPrivilegedRole(actor) ||
                (contact.AssignedAgentId.HasValue && contact.AssignedAgentId.Value == actorUserId);

            if (!canUpdate)
                throw new UnauthorizedAccessException("Not allowed to update this conversation status.");

            contact.InboxStatus = normalized;

            // Back-compat for older query logic
            if (normalized == "Closed")
            {
                contact.IsArchived = true;
                contact.IsActive = false;
            }
            else
            {
                contact.IsArchived = false;
                contact.IsActive = true;
            }

            _logger.LogInformation(
                "ChatInbox status updated. BusinessId={BusinessId} ContactId={ContactId} Status={Status} ActorUserId={ActorUserId}",
                businessId, contactId, normalized, actorUserId);

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await BroadcastRefreshAsync(businessId).ConfigureAwait(false);
        }

        private static string? NormalizeStatus(string? status)
        {
            var raw = (status ?? string.Empty).Trim();
            if (raw.Length == 0) return null;

            var lower = raw.ToLowerInvariant();
            return lower switch
            {
                "open" => "Open",
                "pending" => "Pending",
                "closed" => "Closed",
                _ => null
            };
        }

        private async Task<Contact> LoadBusinessContactAsync(Guid businessId, Guid contactId, CancellationToken ct)
        {
            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId, ct)
                .ConfigureAwait(false);

            if (contact == null)
                throw new InvalidOperationException("Contact not found.");

            return contact;
        }

        private async Task<User> LoadActiveBusinessUserAsync(Guid businessId, Guid userId, CancellationToken ct)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                .ConfigureAwait(false);

            if (user == null)
                throw new InvalidOperationException("User not found.");

            if (user.BusinessId != businessId)
                throw new UnauthorizedAccessException("User does not belong to this business.");

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("User is not active.");

            return user;
        }

        private static bool IsPrivilegedRole(User actor)
        {
            var role = (actor.Role?.Name ?? string.Empty).Trim().ToLowerInvariant();
            return role is "admin" or "business" or "superadmin" or "partner";
        }

        private async Task<bool> CanAssignOthersAsync(User actor, CancellationToken ct)
        {
            if (IsPrivilegedRole(actor)) return true;
            return await HasPermissionAsync(actor.Id, InboxAssignPermissionCode, ct).ConfigureAwait(false);
        }

        private async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct)
        {
            // Permission sources checked:
            // 1) UserPermissions (direct user overrides)
            // 2) RolePermissions (business role mapping)
            var rawCode = (permissionCode ?? string.Empty).Trim();
            var code = rawCode.ToUpperInvariant();
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
                .AnyAsync(
                    up => up.UserId == userId
                          && up.PermissionId == permissionId.Value
                          && up.IsGranted
                          && !up.IsRevoked,
                    ct)
                .ConfigureAwait(false);

            if (direct)
            {
                _logger.LogDebug(
                    "Permission granted via UserPermissions override. UserId={UserId} PermissionCode={PermissionCode}",
                    userId,
                    rawCode);
                return true;
            }

            var roleId = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.RoleId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!roleId.HasValue)
            {
                _logger.LogDebug(
                    "Permission denied (no role). UserId={UserId} PermissionCode={PermissionCode}",
                    userId,
                    rawCode);
                return false;
            }

            var byRole = await _db.RolePermissions
                .AsNoTracking()
                .AnyAsync(
                    rp => rp.RoleId == roleId.Value
                          && rp.PermissionId == permissionId.Value
                          && rp.IsActive
                          && !rp.IsRevoked,
                    ct)
                .ConfigureAwait(false);

            if (byRole)
            {
                _logger.LogDebug(
                    "Permission granted via RolePermissions mapping. UserId={UserId} RoleId={RoleId} PermissionCode={PermissionCode}",
                    userId,
                    roleId.Value,
                    rawCode);
                return true;
            }

            _logger.LogDebug(
                "Permission denied (no direct or role grant). UserId={UserId} RoleId={RoleId} PermissionCode={PermissionCode}",
                userId,
                roleId.Value,
                rawCode);

            return byRole;
        }

        private async Task BroadcastRefreshAsync(Guid businessId)
        {
            try
            {
                await _hub.Clients
                    .Group($"business_{businessId}")
                    .SendAsync("UnreadCountChanged", new { refresh = true })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast inbox refresh. BusinessId={BusinessId}", businessId);
            }
        }
    }
}
