using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// A single member of an Audience. May or may not be linked to a Contact.
    /// </summary>
    public class AudienceMember
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid AudienceId { get; set; }
        public Audience Audience { get; set; } = null!;

        // 🆕 explicit tenant for fast filtering & safety
        [Required] public Guid BusinessId { get; set; }

        /// <summary>Optional CRM link; null for non-CRM rows until promotion</summary>
        public Guid? ContactId { get; set; }

        [MaxLength(64)]
        public string? PhoneRaw { get; set; }

        [MaxLength(32)]
        public string? PhoneE164 { get; set; }

        [MaxLength(160)]
        public string? Name { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }   // 🆕

        /// <summary>Additional attributes from CSV row (json)</summary>
        public string? AttributesJson { get; set; } // keep name as-is

        /// <summary>True if an “auto-created” CRM contact; subject to retention</summary>
        public bool IsTransientContact { get; set; } = false;

        public bool IsDeleted { get; set; } = false;  // 🆕

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }      // 🆕
        public DateTime? PromotedAt { get; set; }     // when transient → durable Contact
        public Guid? CreatedByUserId { get; set; }
    }
}
