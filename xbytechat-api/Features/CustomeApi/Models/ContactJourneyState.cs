using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Features.CustomeApi.Models
{
    // One row per (business, flow, contact). Enforce single row via unique index.
    [Index(nameof(BusinessId), nameof(FlowId), nameof(ContactPhone), IsUnique = true)]
    public class ContactJourneyState
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid BusinessId { get; set; }

        [Required] public Guid FlowId { get; set; }

        // Store digits-only (same as your click processor does).
        [Required, MaxLength(32)]
        public string ContactPhone { get; set; } = default!;

        // Running journey like: "Yes/No/Bahut Achha"
        [Required] public string JourneyText { get; set; } = string.Empty;

        public int ClickCount { get; set; } = 0;

        [MaxLength(256)]
        public string? LastButtonText { get; set; }

        [Required] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
