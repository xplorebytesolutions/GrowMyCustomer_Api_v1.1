using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Directory
{
    /// <summary>
    /// Resolves BusinessId from provider-specific webhook identifiers.
    /// </summary>
    public interface IProviderDirectory
    {
        /// <param name="provider">"meta" or "pinnacle" (lowercase preferred)</param>
        /// <param name="phoneNumberId">Meta/Pinnacle phone_number_id (strongest key)</param>
        /// <param name="displayPhoneNumber">Formatted sending number (e.g. "+91XXXXXXXXXX")</param>
        /// <param name="wabaId">WhatsApp Business Account ID (Meta)</param>
        /// <param name="waId">Optional WA ID (recipient); used only as a last-ditch heuristic</param>
        Task<Guid?> ResolveBusinessIdAsync(
            string? provider,
            string? phoneNumberId,
            string? displayPhoneNumber,
            string? wabaId,
            string? waId,
            CancellationToken ct = default);
    }
}
