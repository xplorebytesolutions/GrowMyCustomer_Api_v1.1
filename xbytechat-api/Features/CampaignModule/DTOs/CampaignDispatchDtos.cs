using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignDispatchResponseDto
    {
        public Guid CampaignId { get; set; }
        public string Mode { get; set; } = "canary"; // canary|full
        public int RequestedCount { get; set; }
        public int SelectedCount { get; set; }
        public int EnqueuedCount { get; set; }
        public List<DispatchedRecipientDto> Sample { get; set; } = new(); // small sample for debug
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class DispatchedRecipientDto
    {
        public Guid RecipientId { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; }
        public DateTime? MaterializedAt { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}
