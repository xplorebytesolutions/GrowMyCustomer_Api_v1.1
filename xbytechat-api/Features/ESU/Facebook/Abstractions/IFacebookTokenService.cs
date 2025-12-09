#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ESU.Facebook.Contracts;

namespace xbytechat.api.Features.ESU.Facebook.Abstractions
{
    /// <summary>Retrieves a Facebook access token for a business with expiry checks and caching.</summary>
    public interface IFacebookTokenService
    {
        /// <summary>
        /// Returns a stored token if it exists and is not near expiry. Returns null if missing/expired.
        /// </summary>
        Task<FacebookStoredToken?> TryGetValidAsync(Guid businessId, CancellationToken ct = default);

        /// <summary>
        /// Throws if missing/expired. Use when a valid token is required for an operation.
        /// </summary>
        Task<FacebookStoredToken> GetRequiredAsync(Guid businessId, CancellationToken ct = default);

        Task<string?> GetAccessTokenAsync(Guid businessId, CancellationToken ct = default);
        Task InvalidateAsync(Guid businessId, CancellationToken ct = default);

    }
}
