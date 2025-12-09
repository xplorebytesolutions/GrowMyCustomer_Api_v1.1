using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignRetryResultDto
    {
        public Guid CampaignId { get; set; }
        public int ConsideredFailed { get; set; }
        public int Retried { get; set; }
        public int Skipped { get; set; }  // e.g., duplicates, already succeeded, or filtered out
        public List<Guid> RecipientIdsSample { get; set; } = new(); // up to 20 IDs for quick inspection
        public string? Note { get; set; }
    }
}
