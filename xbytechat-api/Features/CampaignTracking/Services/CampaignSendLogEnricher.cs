using System.Threading.Tasks;
using xbytechat.api.Features.CampaignTracking.Models;
using System;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public class CampaignSendLogEnricher : ICampaignSendLogEnricher
    {
        public async Task EnrichAsync(CampaignSendLog log, string userAgent, string ipAddress)
        {
            // 🧠 Device Detection (simplified for now)
            log.DeviceInfo = userAgent;

            // 🌍 IP Lookup - Mocked for now
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                log.IpAddress = ipAddress;
                log.SourceChannel = "API"; // Example: mark origin
                // Future: Use IPinfo or GeoLite2 for full location enrichment
            }

            // ⌛ Simulate async task for compatibility
            await Task.CompletedTask;
        }
    }
}

