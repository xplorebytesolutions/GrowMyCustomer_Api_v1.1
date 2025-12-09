using System.Threading.Channels;

namespace xbytechat.api.Features.CampaignTracking.Worker
{
    public sealed class InProcessClickEventQueue : IClickEventQueue
    {
        private readonly Channel<ClickEvent> _ch;

        public InProcessClickEventQueue(int capacity = 20_000)
        {
            _ch = Channel.CreateBounded<ClickEvent>(new BoundedChannelOptions(capacity)
            {
                // keep newest; never block redirect
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public bool TryWrite(ClickEvent evt) => _ch.Writer.TryWrite(evt);

        public async Task<List<ClickEvent>> ReadBatchAsync(int maxItems, TimeSpan wait, CancellationToken ct)
        {
            var list = new List<ClickEvent>(maxItems);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(wait);

            while (list.Count < maxItems && await _ch.Reader.WaitToReadAsync(cts.Token))
            {
                while (list.Count < maxItems && _ch.Reader.TryRead(out var item))
                    list.Add(item);
            }
            return list;
        }
    }
}
