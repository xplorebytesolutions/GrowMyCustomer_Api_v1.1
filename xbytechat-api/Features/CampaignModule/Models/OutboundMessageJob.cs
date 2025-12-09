using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class OutboundMessageJob
    {
        [Key] public Guid Id { get; set; }

        public Guid BusinessId { get; set; }
        public Guid CampaignId { get; set; }
        public Guid RecipientId { get; set; }

        // Exactly what to send
        public string Provider { get; set; } = string.Empty;          // DB value as-is
        public string MediaType { get; set; } = "text";               // text|image|video|document
        public string TemplateName { get; set; } = string.Empty;
        public string LanguageCode { get; set; } 
        public string? PhoneNumberId { get; set; }                    // for Meta

        // Pre-resolved components/materialization (avoid recomputation in worker)
        public string ResolvedParamsJson { get; set; } = "[]";
        public string ResolvedButtonUrlsJson { get; set; } = "[]";
        public string? HeaderMediaUrl { get; set; }                   // image/video/document
        public string? MessageBody { get; set; }                      // optional

        // For idempotency (dedupe retries & restarts)
        public string IdempotencyKey { get; set; } = string.Empty;

        // Job lifecycle
        public string Status { get; set; } = "Pending";               // Pending|InFlight|Sent|Failed|Dead
        public int Attempt { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? NextAttemptAt { get; set; }                  // backoff
        public string? LastError { get; set; }                        // truncated
    }
}
