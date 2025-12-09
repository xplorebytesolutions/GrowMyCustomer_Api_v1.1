using System.Threading.Tasks;
using xbytechat.api.Features.CampaignTracking.Models;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public interface ICampaignSendLogEnricher
    {
        Task EnrichAsync(CampaignSendLog log, string userAgent, string ipAddress);
    }
}

