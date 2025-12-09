using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.Entitlements.Models
{
    [Table("BusinessQuotaOverrides")]
    public sealed class BusinessQuotaOverride
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(128)]
        public string QuotaKey { get; set; } = default!; // same key as PlanQuota

        public long? Limit { get; set; }     // null => fallback to plan
        public bool? IsUnlimited { get; set; } // true => unlimited regardless of plan

        public DateTime? ExpiresAt { get; set; } // null => permanent

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
