using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.ESU.Facebook.Clients
{
    public interface IWabaSubscriptionClient
    {
        Task SubscribeAsync(string wabaId, string accessToken, CancellationToken ct = default);
        Task UnsubscribeAsync(string wabaId, string accessToken, CancellationToken ct = default);
    }
}
