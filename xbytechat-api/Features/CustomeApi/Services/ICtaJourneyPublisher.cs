using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CustomeApi.Services
{
    public interface ICtaJourneyPublisher
    {
        /// <summary>
        /// Posts a CTAJourney event for the given business to all active endpoints in CustomerWebhookConfigs.
        /// </summary>
        Task PublishAsync(Guid businessId, Models.CtaJourneyEventDto dto, CancellationToken ct = default);
    }
}
