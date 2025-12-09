using System;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Services.Resolvers
{
    public interface IMessageIdResolver
    {
        Task<Guid?> ResolveCampaignSendLogIdAsync(string messageId);
        Task<Guid?> ResolveMessageLogIdAsync(string messageId);
        Task<Guid?> ResolveBusinessIdByMessageIdAsync(string messageId);
        Task<string?> ResolveAsync(string providerMessageId, CancellationToken ct = default);

    }
}
