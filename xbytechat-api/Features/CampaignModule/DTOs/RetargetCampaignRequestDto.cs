using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    /// <summary>
    /// Retarget creates a NORMAL campaign in DB but skips audience assignment.
    /// The selected recipients come explicitly from analytics selection.
    /// </summary>
    public class RetargetCampaignRequestDto
    {
        // Source context (optional but useful for audit + future UI)
        public Guid SourceCampaignId { get; set; }
        public Guid? SourceRunId { get; set; }
        public string? SourceBucket { get; set; } // clicked | replied | failed | etc.

        // The "new campaign details" user fills in retarget wizard
        public string? Name { get; set; }
        public CampaignCreateDto Campaign { get; set; } = new();

        // Explicit selection
        public List<Guid> ContactIds { get; set; } = new();      // preferred
        public List<string> RecipientPhones { get; set; } = new(); // fallback for rows without ContactId

        // Optional: safety
        public bool Deduplicate { get; set; } = true;
    }

    public class RetargetCampaignResponseDto
    {
        public Guid NewCampaignId { get; set; }
        public int MaterializedRecipients { get; set; }
        public int SkippedDuplicates { get; set; }
    }
}
