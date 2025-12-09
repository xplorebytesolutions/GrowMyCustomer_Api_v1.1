using System;

namespace xbytechat.api.Features.Tracking.DTOs
{
    public class TrackingLogDto
    {
        // 🧩 Multi-Tenant Isolation
        public Guid BusinessId { get; set; }

        // 👤 CRM Linkage
        public Guid? ContactId { get; set; }
        public string? ContactPhone { get; set; }

        // 🔗 Source Info
        public string SourceType { get; set; } = string.Empty; // e.g. "campaign", "reminder", "bot"
        public Guid? SourceId { get; set; }

        // 🔘 CTA Info
        public string? ButtonText { get; set; }
        public string? CTAType { get; set; }

        // 📨 Message Context
        public string? MessageId { get; set; }
        public string? TemplateId { get; set; }
        public Guid? MessageLogId { get; set; }

        // 🧠 Meta / Behaviour
        public string? ClickedVia { get; set; }
        public string? Referrer { get; set; }
        public DateTime? ClickedAt { get; set; } = DateTime.UtcNow;
        // 📡 Tracking
        public string? IPAddress { get; set; }
        public string? Browser { get; set; }
        public string? DeviceType { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }

        // 🔖 Session context
        public string? SessionId { get; set; }
        public string? ThreadId { get; set; }

        public Guid? CampaignId { get; set; }
        public Guid? CampaignSendLogId { get; set; }

        public string RawJson { get; set; } = string.Empty; // used in queue method
        public DateTime EnqueuedAt { get; set; } // used in queue method
        public string? NextStepMatched { get; set; } // ✅ Add this if not already there

        public string? TemplateName { get; set; } // ✅ Needed for follow-up matcher


    }
}
