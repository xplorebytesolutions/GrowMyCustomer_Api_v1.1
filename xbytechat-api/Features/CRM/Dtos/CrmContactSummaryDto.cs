using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CRM.DTOs
{
    public sealed class CrmContactSummaryDto
    {
        public Guid ContactId { get; set; }
        public Guid BusinessId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? LeadSource { get; set; }
        public DateTime? CreatedAt { get; set; }

        // 👇 These names match ChatInbox.jsx usage
        public List<CrmTagChipDto> Tags { get; set; } = new();
        public List<CrmNoteSnippetDto> RecentNotes { get; set; } = new();
        public CrmReminderSnippetDto? NextReminder { get; set; }
        public List<CrmTimelineEventDto> RecentTimeline { get; set; } = new();
    }

    public sealed class CrmTagChipDto
    {
        public Guid Id { get; set; }
        public string TagName { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
    }

    public sealed class CrmNoteSnippetDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CrmReminderSnippetDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Status { get; set; }
        public DateTime DueAt { get; set; }
        public int? Priority { get; set; }
    }

    public sealed class CrmTimelineEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? Category { get; set; }
        public string? EventType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
