using System;

namespace xbytechat.api.Features.CRM.Timelines.DTOs
{
    public class LeadTimelineDto
    {
        public Guid ContactId { get; set; }
        public string ContactName { get; set; }
        public string ContactNumber { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string? Data { get; set; }
        public Guid? ReferenceId { get; set; }
        public bool IsSystemGenerated { get; set; } = false;
        public string CreatedBy { get; set; }
        public string? Source { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
