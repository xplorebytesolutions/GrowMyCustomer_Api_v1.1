// 📄 File: xbytechat.api/Features/CTAFlowBuilder/Models/CTAFlowConfig.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    /// <summary>
    /// Represents a complete flow configuration for a business, such as "Interested Journey".
    /// </summary>
    public class CTAFlowConfig
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FlowName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsPublished { get; set; } = false; // ✅ NEW: Support draft/published

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }  // ✅ Add this line

        // 🔁 Navigation to steps
        public ICollection<CTAFlowStep> Steps { get; set; } = new List<CTAFlowStep>();
    }
}

// 📄 File: xbytechat.api/Features/CTAFlowBuilder/Models/CTAFlowConfig.cs
//using System.ComponentModel.DataAnnotations;
//using System.Text.Json.Serialization;
//using Microsoft.EntityFrameworkCore;

//namespace xbytechat.api.Features.CTAFlowBuilder.Models
//{
//    /// <summary>
//    /// Represents a complete flow configuration for a business, such as "Interested Journey".
//    /// </summary>
//    [Index(nameof(BusinessId), nameof(IsActive), nameof(FlowName), Name = "ix_ctaflowconfigs_biz_active_name")]
//    [Index(nameof(BusinessId), nameof(IsPublished), Name = "ix_ctaflowconfigs_biz_published")]
//    public class CTAFlowConfig
//    {
//        [Key]
//        public Guid Id { get; set; }

//        [Required]
//        public Guid BusinessId { get; set; }

//        [Required, MaxLength(100)]
//        public string FlowName { get; set; } = string.Empty;

//        /// <summary>
//        /// Soft “enabled/disabled” flag for listing/selection. We still hard-delete unused flows on request.
//        /// </summary>
//        public bool IsActive { get; set; } = true;

//        /// <summary>
//        /// Draft vs published for the builder.
//        /// </summary>
//        public bool IsPublished { get; set; } = false;

//        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//        public string? CreatedBy { get; set; }

//        /// <summary>
//        /// Last modification timestamp (updated in service on edits).
//        /// </summary>
//        public DateTime? UpdatedAt { get; set; }

//        /// <summary>
//        /// Optimistic concurrency token to avoid race conditions (e.g., editing while someone tries to delete).
//        /// </summary>
//        [Timestamp]
//        public byte[]? RowVersion { get; set; }

//        // 🔁 Navigation to steps
//        // Cascade delete is configured in OnModelCreating:
//        // modelBuilder.Entity<CTAFlowConfig>()
//        //   .HasMany(f => f.Steps).WithOne(s => s.Flow)
//        //   .HasForeignKey(s => s.FlowId)
//        //   .OnDelete(DeleteBehavior.Cascade);
//        [JsonIgnore] // prevent huge payloads if you serialize configs somewhere else
//        public ICollection<CTAFlowStep> Steps { get; set; } = new List<CTAFlowStep>();
//    }
//}

