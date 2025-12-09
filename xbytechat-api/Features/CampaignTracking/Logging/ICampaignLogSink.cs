using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CampaignTracking.Logging
{
    public interface ICampaignLogSink
    {
        void Enqueue(CampaignLogRecord rec);
        int PendingCount { get; }
        Task FlushAsync(CancellationToken ct = default);
    }
}
