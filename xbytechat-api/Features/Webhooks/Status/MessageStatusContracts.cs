namespace xbytechat.api.Features.Webhooks.Status
{
    public class MessageStatusContracts
    {
        public enum CanonicalMessageStatus
        {
            Unknown = 0,
            Submitted,   // API accepted (optional)
            Sent,        // provider accepted / sent
            Delivered,
            Read,
            Failed
        }
        public sealed class UpdateMessageStatusRequest
        {
            public Guid BusinessId { get; set; }
            public string Provider { get; set; } = "";              // "meta_cloud" | "pinnacle" | etc.
            public string MessageId { get; set; } = "";             // provider message id (WAMID / id)
            public string RawStatus { get; set; } = "";             // provider-specific (e.g., "sent", "delivered")
            public DateTimeOffset? EventTime { get; set; }          // provider timestamp, if any

            public string? RecipientNumber { get; set; }            // optional sanity context
            public string? ErrorCode { get; set; }                  // optional error info
            public string? ErrorMessage { get; set; }               // optional error info
            public string? RawPayloadJson { get; set; }             // optional audit/debug
        }

        public interface IMessageStatusUpdater
        {
            Task<bool> UpdateAsync(UpdateMessageStatusRequest req, CancellationToken ct = default);
        }
    }
}
