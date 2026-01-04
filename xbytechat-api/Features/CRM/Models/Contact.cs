using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.BusinessModule.Models;

namespace xbytechat.api.Features.CRM.Models
{
    public class Contact
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessId { get; set; }
        public Business Business { get; set; } = null!;
        // 🔗 FK to Business
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? LeadSource { get; set; }

        [MaxLength(200)]
        public string? Tags { get; set; } // Legacy, will be deprecated after ContactTag rollout

        public DateTime? LastContactedAt { get; set; }
        public DateTime? NextFollowUpAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🧩 NEW: Link to Tags
        public ICollection<ContactTag> ContactTags { get; set; } = new List<ContactTag>();
        // ✅ New: Navigation property for many-to-many tags
        //public ICollection<ContactTag> TagsLink { get; set; } = new List<ContactTag>();

        public DateTime? LastCTAInteraction { get; set; }
        public string? LastCTAType { get; set; }
        public Guid? LastClickedProductId { get; set; }

        // 🚦 If true, skip automation flows (manually or programmatically paused)
        public bool IsAutomationPaused { get; set; } = false;

        // 👤 If agent assigned, automation should pause (runtime check)
        public Guid? AssignedAgentId { get; set; }

        // --- Chat Inbox fields ---
        [MaxLength(20)]
        public string InboxStatus { get; set; } = "Open"; // Open / Pending / Closed

        public DateTime? LastInboundAt { get; set; }   // UTC
        public DateTime? LastOutboundAt { get; set; }  // UTC

        public bool IsFavorite { get; set; } = false;
        public bool IsArchived { get; set; } = false;
        public string? Group { get; set; }
        public bool IsActive { get; set; } = true;


        //public bool IsTemporary { get; set; } = false;
        //public Guid? SourceCampaignId { get; set; }
        //public DateTime? ExpiresAt { get; set; }

        public string? ProfileName { get; set; }            // latest WA profile.name we saw inbound
        public DateTime? ProfileNameUpdatedAt { get; set; }

    }
}
