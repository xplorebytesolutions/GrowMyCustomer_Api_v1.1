using System;

namespace xbytechat.api.Features.Webhooks.DTOs
{
    public class FailedWebhookLogDto
    {
        public string? ErrorMessage { get; set; }
        public string? SourceModule { get; set; }
        public string? FailureType { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
