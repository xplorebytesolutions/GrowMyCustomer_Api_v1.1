using System;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignProgressDto
    {
        public Guid CampaignId { get; set; }
        public int TotalJobs { get; set; }
        public int Pending { get; set; }
        public int InFlight { get; set; }
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int Dead { get; set; }
        public int Completed { get; set; }
        public double CompletionPct { get; set; }
        public double? P50ms { get; set; }
        public double? P95ms { get; set; }
        public double? P99ms { get; set; }
        public DateTime RetrievedAtUtc { get; set; }
    }
}



//namespace xbytechat.api.Features.CampaignModule.Progress.Dtos
//{
//    public sealed class CampaignProgressDto
//    {
//        public Guid CampaignId { get; init; }

//        // Queue / job state
//        public int TotalJobs { get; init; }
//        public int Pending { get; init; }
//        public int InFlight { get; init; }
//        public int Sent { get; init; }
//        public int Failed { get; init; }
//        public int Dead { get; init; }

//        // Derived
//        public int Completed => Sent + Failed + Dead;
//        public double CompletionPct => TotalJobs == 0 ? 0 : (double)Completed / TotalJobs * 100.0;

//        // Last-hour send latency (ms)
//        public double? P50ms { get; init; }
//        public double? P95ms { get; init; }
//        public double? P99ms { get; init; }

//        public DateTime RetrievedAtUtc { get; init; }
//    }
//}