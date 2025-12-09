using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.PlanManagement.Models
{
    [Table("PlanFeatureMatrix")]
    public class PlanFeatureMatrix
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string PlanName { get; set; } = string.Empty;  // "Basic", "Smart", "Advance"

        [Required]
        [MaxLength(50)]
        public string FeatureName { get; set; } = string.Empty; // "Contacts", "Catalog", etc.

        [Required]
        public bool IsEnabled { get; set; }  // Default state for this plan-feature pair
    }
}
