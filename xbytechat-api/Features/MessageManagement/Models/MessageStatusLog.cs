using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.BusinessModule.Models;


namespace xbytechat.api.Features.MessageManagement.DTOs
{
    public class MessageStatusLog
    {
        [Key]
        public Guid Id { get; set; }

        // 🔗 Who is it for
        public string RecipientNumber { get; set; }
        public string? CustomerProfileName { get; set; }

        // 📩 WhatsApp Message Info
       // [ForeignKey(nameof(Message))]
        public string? MessageId { get; set; } // WAMID

       // public MessageLog? Message { get; set; }
        public string Status { get; set; }
        public string MessageType { get; set; }

        // 🧾 Template Info
        public string? TemplateName { get; set; }
        public string? TemplateCategory { get; set; }

        // 🧠 Analytics/Reporting
        public string Channel { get; set; } = "whatsapp";
        public bool IsSessionOpen { get; set; }
        public long? MetaTimestamp { get; set; }

        // 🕒 Timestamps
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        // ❌ Error Tracking
        public string? ErrorMessage { get; set; }
        public int? ErrorCode { get; set; }

        // 🔍 Raw Log (for audit/debug)
        public string? RawPayload { get; set; }

        // 🔗 Foreign Keys
        public Guid? CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        public Guid? BusinessId { get; set; }
        public Business? Business { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  

    }
}
