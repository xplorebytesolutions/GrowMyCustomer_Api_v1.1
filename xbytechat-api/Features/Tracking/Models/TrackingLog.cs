using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Tracking.Models;
namespace xbytechat.api.Features.Tracking.Models
{
    public class TrackingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🧩 Multi-Tenant Isolation
        public Guid BusinessId { get; set; }

        // 👤 CRM Linkage
        public Guid? ContactId { get; set; }
        public string? ContactPhone { get; set; }
        public Contact? Contact { get; set; } // ✅ NEW

        // 🔗 Source Info
        public string SourceType { get; set; } = string.Empty;
        public Guid? SourceId { get; set; }

        public Guid? CampaignId { get; set; }
        public Campaign? Campaign { get; set; } // ✅ NEW

        public Guid? CampaignSendLogId { get; set; }
        public CampaignSendLog? CampaignSendLog { get; set; } // ✅ Optional

        // 🔘 Button Info
        public string? ButtonText { get; set; }
        public string? CTAType { get; set; }

        // 📨 Message Context
        public string? MessageId { get; set; }
        public string? TemplateId { get; set; }
        public Guid? MessageLogId { get; set; }
        public MessageLog? MessageLog { get; set; } // ✅ Optional

        // 🧠 Meta / Behaviour
        public string? ClickedVia { get; set; }
        public string? Referrer { get; set; }

        // 🕒 Audit Trail
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public string? IPAddress { get; set; }
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }

        // 🔖 Follow-up & Analytics
        public bool FollowUpSent { get; set; } = false;
        public string? LastInteractionType { get; set; }

        // 🧵 Journey Tracking
        public Guid? SessionId { get; set; }
        public Guid? ThreadId { get; set; }
        public Guid? StepId { get; set; } // ✅ Link to CTAFlowStep for CTA Flow tracking

       //  public string? NextStepMatched { get; set; } // Logs which template system resolved
    }
}
