using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.ESU.Facebook.Models
{
    /// <summary>
    /// One row per Business capturing lightweight “connected” flags
    /// and optional token metadata for ESU-style integrations.
    /// </summary>
    [Table("IntegrationFlags")]
    public sealed class IntegrationFlags
    {   
        [Key]
        public Guid BusinessId { get; set; }

        // --- Facebook ESU ---
        public bool FacebookEsuCompleted { get; set; }

        // Optional: store a short-lived user token value/expiry if you plan follow-up Graph calls.
        // Keep nullable; app can run just with the completion flag.
        //[MaxLength(2048)]
        //public string? FacebookAccessToken { get; set; }

        //public DateTime? FacebookTokenExpiresAtUtc { get; set; }

        // housekeeping
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
