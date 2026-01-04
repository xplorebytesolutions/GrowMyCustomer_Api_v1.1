using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// Logical grouping of recipients (often tied to a CsvBatch).
    /// A campaign can materialize recipients from one Audience.
    /// </summary>
    public class Audience
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid BusinessId { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = "Untitled Audience";

        [MaxLength(512)]
        public string? Description { get; set; }  // useful in UI
               
        //public Guid? CampaignId { get; set; }
        //public Campaign? Campaign { get; set; }

        public Guid? CsvBatchId { get; set; }
        public CsvBatch? CsvBatch { get; set; }

        public bool IsDeleted { get; set; } = false;

        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }   // audit

        public ICollection<AudienceMember> Members { get; set; } = new List<AudienceMember>();
    }
}
