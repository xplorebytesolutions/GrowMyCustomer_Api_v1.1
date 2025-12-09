using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.Entitlements.Models;

namespace xbytechat.api.Features.Entitlements.Models
{
    [Table("BusinessUsageCounters")]
    public sealed class BusinessUsageCounter
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(128)]
        public string QuotaKey { get; set; } = default!;

        public QuotaPeriod Period { get; set; }

        // To support resets, store the window start for this counter.
        public DateTime WindowStartUtc { get; set; }

        // Current consumed units within the window.
        public long Consumed { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
