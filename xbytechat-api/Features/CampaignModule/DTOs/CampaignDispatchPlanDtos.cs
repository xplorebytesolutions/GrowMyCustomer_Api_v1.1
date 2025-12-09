using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class DispatchBatchDto
    {
        public int BatchNumber { get; set; }
        public int StartIndex { get; set; }
        public int Count { get; set; }

        /// <summary>Total approximate payload size for this batch in bytes (text + buttons, naive estimate).</summary>
        public int ApproxBytes { get; set; }

        /// <summary>Seconds since plan start when this batch is allowed to start (based on throttling).</summary>
        public int OffsetSeconds { get; set; }

        public List<Guid?> RecipientIds { get; set; } = new();   // when using CampaignRecipients
        public List<string?> Phones { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class DispatchThrottleDto
    {
        public string Plan { get; set; } = "Unknown";
        public string Provider { get; set; } = "Auto";
        public int MaxBatchSize { get; set; } = 50;
        public int MaxPerMinute { get; set; } = 300;
        public int ComputedBatches { get; set; }
        public int EstimatedMinutes { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class CampaignDispatchPlanResultDto
    {
        public Guid CampaignId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
        public int PlaceholderCount { get; set; }

        public int TotalRecipients { get; set; }
        public int TotalApproxBytes { get; set; }

        public DispatchThrottleDto Throttle { get; set; } = new();
        public List<DispatchBatchDto> Batches { get; set; } = new();

        public int WarningCount { get; set; }
        public List<string> GlobalWarnings { get; set; } = new();
    }
}
