using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    public interface IAutoReplyRuntimeService
    {
        /// <summary>
        /// Real runtime handler used by the webhook when an inbound WhatsApp message arrives.
        /// </summary>
        Task<AutoReplyRuntimeResult> TryHandleAsync(
            Guid businessId,
            Guid contactId,
            string contactPhone,
            string incomingText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Test-only match used by the AutoReply Builder UI ("Test Auto-Reply Match" panel).
        /// MUST NOT send any real messages.
        /// </summary>
        Task<AutoReplyRuntimeResult> TestMatchAsync(
            Guid businessId,
            string incomingText,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Legacy DTO-based matcher used by existing endpoints
        /// (e.g. /api/auto-reply-runtime/button-click).
        /// Thin adapter over <see cref="TestMatchAsync"/>.
        /// </summary>
        Task<AutoReplyMatchResultDto> FindMatchAsync(
            AutoReplyMatchRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
