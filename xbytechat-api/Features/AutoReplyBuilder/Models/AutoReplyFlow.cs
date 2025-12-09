using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.AutoReplyBuilder.Flows.Models
{
    public class AutoReplyFlow
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string NodesJson { get; set; } = string.Empty;

        [Required]
        public string EdgesJson { get; set; } = string.Empty;

        // Stored in UTC; set on first create
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Nullable; updated on every SaveFlowAsync
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Comma / newline separated trigger keywords, e.g. "hi, hello"
        /// </summary>
        public string? TriggerKeyword { get; set; }

        public bool IsActive { get; set; } = true;

        // Optional metadata for templates / catalog
        public string? IndustryTag { get; set; }    // e.g., "restaurant", "clinic", "education"
        public string? UseCase { get; set; }        // e.g., "Order Flow", "Booking Flow"

        /// <summary>
        /// Flag to indicate system-provided template (vs user-created).
        /// </summary>
        public bool IsDefaultTemplate { get; set; } = false;

        /// <summary>
        /// Matching mode for this flow:
        /// "Exact" | "Word" | "StartsWith" | "Contains".
        /// Default: "Word".
        /// </summary>
        [MaxLength(32)]
        public string MatchMode { get; set; } = "Word";

        /// <summary>
        /// Priority for choosing between multiple matching flows.
        /// Higher value wins.
        /// Default: 0.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Legacy alias for TriggerKeyword; still populated for older code paths.
        /// </summary>
        public string? Keyword { get; set; }
    }
}




//using System;
//using System.ComponentModel.DataAnnotations;

//namespace xbytechat.api.Features.AutoReplyBuilder.Flows.Models
//{
//    public class AutoReplyFlow
//    {
//        [Key]
//        public Guid Id { get; set; }

//        public Guid BusinessId { get; set; }

//        [Required]
//        public string Name { get; set; } = string.Empty;

//        [Required]
//        public string NodesJson { get; set; } = string.Empty;

//        [Required]
//        public string EdgesJson { get; set; } = string.Empty;

//        // Stored in UTC; set on first create
//        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

//        // Nullable; updated on every SaveFlowAsync
//        public DateTime? UpdatedAt { get; set; }

//        /// <summary>
//        /// Comma / newline separated trigger keywords, e.g. "hi, hello"
//        /// </summary>
//        public string? TriggerKeyword { get; set; }

//        public bool IsActive { get; set; } = true;

//        // Optional metadata for templates / catalog
//        public string? IndustryTag { get; set; }    // e.g., "restaurant", "clinic", "education"
//        public string? UseCase { get; set; }        // e.g., "Order Flow", "Booking Flow"

//        /// <summary>
//        /// Flag to indicate system-provided template (vs user-created).
//        /// </summary>
//        public bool IsDefaultTemplate { get; set; } = false;

//        /// <summary>
//        /// Legacy alias for TriggerKeyword; still populated for older code paths.
//        /// </summary>
//        public string? Keyword { get; set; }
//    }
//}
