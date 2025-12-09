using System.Threading.Channels;

namespace XByteChat.api.Features.CampaignModule.Outbox;

public static class OutboxChannels
{
    public static readonly Channel<OutboundItem> SendChannel =
        Channel.CreateBounded<OutboundItem>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.Wait });

    public static readonly Channel<MessageLog> LogChannel =
        Channel.CreateBounded<MessageLog>(new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropOldest });
}
