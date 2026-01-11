// 📄 xbytechat-api/Features/ChatInbox/Controllers/ChatInboxController.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Models;
using xbytechat.api.Features.ChatInbox.Services;
using xbytechat.api.Models;

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
        private readonly IChatInboxMediaUploadService _mediaUploadService;
        private readonly IChatInboxMediaContentService _mediaContentService;
        private readonly AppDbContext _db;
        private readonly ILogger<ChatInboxController> _logger;

        public ChatInboxController(
            IChatInboxQueryService queryService,
            IChatInboxCommandService commandService,
            IChatInboxAssignmentService assignmentService,
            IChatInboxMediaUploadService mediaUploadService,
            IChatInboxMediaContentService mediaContentService,
            AppDbContext db,
            ILogger<ChatInboxController> logger)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));
            _mediaUploadService = mediaUploadService ?? throw new ArgumentNullException(nameof(mediaUploadService));
            _mediaContentService = mediaContentService ?? throw new ArgumentNullException(nameof(mediaContentService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private const long MaxUploadBytes = 10 * 1024 * 1024; // 10MB
        private const string InboxAssignPermissionCode = "INBOX.CHAT.ASSIGN";

        private static readonly HashSet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "application/pdf",
            "video/mp4",
            "video/3gpp",
            "audio/mpeg",
            "audio/mp4",
            "audio/aac",
            "audio/ogg"
        };

        [HttpPost("media/upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ChatInboxMediaUploadResponseDto), 200)]
        public async Task<ActionResult<ChatInboxMediaUploadResponseDto>> UploadMedia(
            [FromForm] ChatInboxMediaUploadRequestDto form,
            CancellationToken ct = default)
        {
            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");

            var file = form?.File;
            if (file == null) return BadRequest("file is required.");
            if (file.Length <= 0) return BadRequest("file is empty.");
            if (file.Length > MaxUploadBytes) return BadRequest($"file is too large. Max allowed is {MaxUploadBytes / (1024 * 1024)}MB.");

            // Some browsers append parameters like "audio/ogg; codecs=opus"
            var mimeRaw = (file.ContentType ?? string.Empty).Trim();
            var mime = mimeRaw.Split(';', 2)[0].Trim();
            if (string.IsNullOrWhiteSpace(mime) || !AllowedContentTypes.Contains(mime))
                return BadRequest("Unsupported file type. Allowed: image/jpeg, image/png, image/webp, application/pdf, video/mp4, audio/mpeg, audio/mp4, audio/aac, audio/ogg.");

            var mediaType = string.Equals(mime, "application/pdf", StringComparison.OrdinalIgnoreCase)
                ? "document"
                : mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    ? "video"
                    : mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                        ? "audio"
                        : "image";

            var safeName = Path.GetFileName(file.FileName ?? "upload.bin");

            try
            {
                var mediaId = await _mediaUploadService.UploadToWhatsAppAsync(
                    tokenBiz.Value,
                    phoneNumberId: null,
                    file,
                    ct).ConfigureAwait(false);

                return Ok(new ChatInboxMediaUploadResponseDto
                {
                    MediaId = mediaId,
                    MediaType = mediaType,
                    FileName = safeName,
                    MimeType = mime,
                    SizeBytes = file.Length
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatInbox media upload failed. BusinessId={BusinessId}", tokenBiz.Value);
                return BadRequest(new { message = "Media upload failed. Please try again." });
            }
        }

        [HttpGet("media/{mediaId}/content")]
        public async Task<IActionResult> GetMediaContent(
            [FromRoute] string mediaId,
            CancellationToken ct = default)
        {
            var tokenBiz = GetBusinessId();
            if (!tokenBiz.HasValue) return Unauthorized("businessId missing in token.");

            var tokenUserId = GetUserId();
            if (!tokenUserId.HasValue) return Unauthorized("userId missing in token.");

            var mid = (mediaId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(mid)) return BadRequest("mediaId is required.");

            var row = await _db.MessageLogs
                .AsNoTracking()
                .Where(m =>
                    m.BusinessId == tokenBiz.Value &&
                    m.MediaId != null &&
                    m.MediaId == mid &&
                    m.ContactId != null)
                .OrderByDescending(m => m.SentAt ?? m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => new
                {
                    ContactId = m.ContactId!.Value,
                    m.FileName,
                    m.MimeType,
                    m.MediaType
                })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (row == null) return NotFound(new { message = "Media not found." });

            try
            {
                await EnsureCanAccessContactAsync(tokenBiz.Value, tokenUserId.Value, row.ContactId, ct)
                    .ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }

            try
            {
                var (stream, contentType) = await _mediaContentService
                    .DownloadFromWhatsAppAsync(tokenBiz.Value, mid, ct)
                    .ConfigureAwait(false);

                var finalType = !string.IsNullOrWhiteSpace(row.MimeType)
                    ? row.MimeType!
                    : (contentType ?? "application/octet-stream");

                var fallbackName = string.Equals(row.MediaType, "document", StringComparison.OrdinalIgnoreCase)
                    ? "document.pdf"
                    : string.Equals(row.MediaType, "video", StringComparison.OrdinalIgnoreCase)
                        ? "video.mp4"
                        : string.Equals(row.MediaType, "audio", StringComparison.OrdinalIgnoreCase)
                            ? "audio"
                            : "image";

                var safeName = Path.GetFileName(row.FileName ?? fallbackName);
                Response.Headers["Cache-Control"] = "no-store";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeName}\"";

                return File(stream, finalType);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatInbox media proxy failed. BusinessId={BusinessId} MediaId={MediaId}", tokenBiz.Value, mid);
                return BadRequest(new { message = "Failed to load media. Please try again." });
            }
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

            // Multi-tenant safety: never trust BusinessId from client; token wins.
            request.BusinessId = tokenBiz.Value;

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

        private async Task EnsureCanAccessContactAsync(
            Guid businessId,
            Guid userId,
            Guid contactId,
            CancellationToken ct)
        {
            var visibility = await _db.Businesses
                .AsNoTracking()
                .Where(b => b.Id == businessId)
                .Select(b => (InboxVisibilityMode?)b.InboxVisibilityMode)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false) ?? InboxVisibilityMode.SharedInInbox;

            if (visibility != InboxVisibilityMode.AssignedOnly) return;

            var canSeeAll = await CanSeeAllInRestrictedModeAsync(businessId, userId, ct).ConfigureAwait(false);
            if (canSeeAll) return;

            var allowed = await _db.Contacts
                .AsNoTracking()
                .AnyAsync(c =>
                    c.BusinessId == businessId &&
                    c.Id == contactId &&
                    c.AssignedAgentId == userId, ct)
                .ConfigureAwait(false);

            if (!allowed)
                throw new UnauthorizedAccessException("Restricted inbox: you are not assigned to this conversation.");
        }

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
