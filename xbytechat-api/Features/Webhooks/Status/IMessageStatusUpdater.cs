// 📄 File: Features/Webhooks/Status/IMessageStatusUpdater.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.Webhooks.Status
{
    public interface IMessageStatusUpdater
    {
        /// <summary>
        /// ✅ Only supported method (industry-grade):
        /// Business-scoped status update to prevent cross-tenant collisions.
        /// </summary>
        Task<int> UpdateAsync(StatusEvent ev, CancellationToken ct = default);
    }

    public sealed class StatusEvent
    {
        public Guid BusinessId { get; init; }

        /// <summary>Provider key: "META_CLOUD", "meta_cloud", "pinnacle", etc.</summary>
        public string Provider { get; init; } = "";

        /// <summary>
        /// Provider message id (Meta: WAMID / "id").
        /// We map this to MessageLog.ProviderMessageId (or MessageId fallback).
        /// </summary>
        public string ProviderMessageId { get; init; } = "";

        /// <summary>
        /// Optional: if you already know the CampaignSendLog row, pass it here for exact targeting.
        /// </summary>
        public Guid? CampaignSendLogId { get; init; }

        public string? RecipientWaId { get; init; }

        public MessageDeliveryState State { get; init; }
        public DateTimeOffset OccurredAt { get; init; }

        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ConversationId { get; init; }
    }

    public enum MessageDeliveryState
    {
        Sent,
        Delivered,
        Read,
        Failed,
        Deleted
    }
}
