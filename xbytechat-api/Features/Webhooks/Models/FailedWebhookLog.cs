using System;

namespace xbytechat.api.Features.Webhooks.Models
{
    public class FailedWebhookLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🧠 Debug Metadata
        public string? ErrorMessage { get; set; }
        public string? SourceModule { get; set; } // e.g., StatusProcessor, ClickProcessor
        public string? FailureType { get; set; }  // e.g., JSON_PARSE_ERROR, DB_LOOKUP_FAILED

        // 📦 Raw Data Snapshot
        public string RawJson { get; set; } = string.Empty;

        // 🕒 Timeline
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
