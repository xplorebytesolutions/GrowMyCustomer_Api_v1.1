using System;

namespace xbytechat.api.Features.Queueing.DTOs
{
    /// <summary>
    /// Minimal job creation payload for outbound campaign sends.
    /// The worker will hydrate the template parameters from CampaignRecipient.
    /// </summary>
    public sealed class OutboundCampaignJobCreateDto
    {
        public Guid BusinessId { get; set; }
        public Guid CampaignId { get; set; }
        public Guid CampaignRecipientId { get; set; }

        /// <summary>Deduplication key. The queue should drop duplicates with the same key.</summary>
        public string? IdempotencyKey { get; set; }
    }
}
