using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class CampaignRecipient
    {
        public Guid Id { get; set; }

        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }   // nav is optional at runtime

        public Guid? ContactId { get; set; }      // ← optional FK
        public Contact? Contact { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Sent, Delivered, Failed, Replied
        public DateTime? SentAt { get; set; }

        public string? BotId { get; set; }
        public string? MessagePreview { get; set; }
        public string? ClickedCTA { get; set; }
        public string? CategoryBrowsed { get; set; }
        public string? ProductBrowsed { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsAutoTagged { get; set; } = false;

        // Logs
        public ICollection<CampaignSendLog> SendLogs { get; set; } = new List<CampaignSendLog>();

        public Guid BusinessId { get; set; }
        public Business? Business { get; set; }

        public Guid? AudienceMemberId { get; set; }
        public AudienceMember? AudienceMember { get; set; } = null!;

        [Column(TypeName = "jsonb")]
        public string? ResolvedParametersJson { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ResolvedButtonUrlsJson { get; set; }

        public string? IdempotencyKey { get; set; }
        public DateTime? MaterializedAt { get; set; }
    }
}


