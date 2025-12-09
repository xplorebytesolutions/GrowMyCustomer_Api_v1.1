using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.Entitlements.Models;

namespace xbytechat.api.Features.Entitlements.Models
{
    [Table("PlanQuotas")]
    public sealed class PlanQuota
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid PlanId { get; set; } // FK to AccessControl Plan

        // Case-insensitive programmatic key, e.g., "MessagesPerMonth"
        [Required, MaxLength(128)]
        public string QuotaKey { get; set; } = default!;

        public long Limit { get; set; }            // -1 => unlimited
        public QuotaPeriod Period { get; set; }    // Daily/Monthly/Lifetime

        // Optional UX copy shown to user on denial
        [MaxLength(256)]
        public string? DenialMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
