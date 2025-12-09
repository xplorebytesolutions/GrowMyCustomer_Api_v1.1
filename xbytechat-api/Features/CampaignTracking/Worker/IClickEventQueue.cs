// Features/CampaignTracking/Worker/IClickEventQueue.cs
namespace xbytechat.api.Features.CampaignTracking.Worker
{
    public interface IClickEventQueue
    {
        bool TryWrite(ClickEvent evt);
        Task<List<ClickEvent>> ReadBatchAsync(int maxItems, TimeSpan wait, CancellationToken ct);
    }
}
