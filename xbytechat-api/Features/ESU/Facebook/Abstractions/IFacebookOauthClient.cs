#nullable enable
using System.Threading;
using System.Threading.Tasks;
using FbContracts = xbytechat.api.Features.ESU.Facebook.Contracts;

namespace xbytechat.api.Features.ESU.Facebook.Abstractions
{
    /// <summary>Handles the OAuth "code → access_token" exchanges with Facebook Graph API.</summary>
    public interface IFacebookOauthClient
    {
        Task<FbContracts.FacebookTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default);

        /// <summary>Swap short-lived token for a ~60-day long-lived token.</summary>
        Task<FbContracts.FacebookTokenResponse> ExchangeForLongLivedAsync(
            FbContracts.FacebookTokenResponse shortToken,
            CancellationToken ct = default);
    }
}
