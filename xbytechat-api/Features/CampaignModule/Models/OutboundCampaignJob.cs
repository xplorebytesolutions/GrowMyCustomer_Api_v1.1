using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignModule.Models
{
    /// <summary>
    /// Queue item to send a whole campaign. Worker will call CampaignService to send.
    /// </summary>
    public class OutboundCampaignJob
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        public Guid CampaignId { get; set; }

        /// <summary>
        /// queued | running | succeeded | failed
        /// </summary>
        [MaxLength(32)]
        public string Status { get; set; } = "queued";

        /// <summary>
        /// Number of send attempts performed.
        /// </summary>
        public int Attempt { get; set; } = 0;

        /// <summary>
        /// Max attempts before we mark failed.
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// When this job becomes eligible for pickup (for backoff).
        /// </summary>
        public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Last error string (truncated in service).
        /// </summary>
        [MaxLength(4000)]
        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
