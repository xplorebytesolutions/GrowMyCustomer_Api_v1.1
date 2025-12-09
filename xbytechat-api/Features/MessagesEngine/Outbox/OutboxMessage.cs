using System;

namespace xbytechat.api.Features.MessagesEngine.Outbox
{
    public enum OutboxStatus
    {
        Queued = 0,
        Sending = 1,
        Sent = 2,
        Failed = 3
    }

    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid? CampaignId { get; set; }
        public Guid? ContactId { get; set; }

        public string RecipientNumber { get; set; } = "";
        public string ProviderKey { get; set; } = ""; // "meta_cloud" | "pinnacle" (optional hint)
        public string PayloadJson { get; set; } = ""; // serialized MessageEnvelope
        public string CorrelationId { get; set; } = ""; // for idempotency & tracing

        public OutboxStatus Status { get; set; } = OutboxStatus.Queued;
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? NextAttemptAt { get; set; } = DateTime.UtcNow;
        public string? LastError { get; set; }
    }
}
