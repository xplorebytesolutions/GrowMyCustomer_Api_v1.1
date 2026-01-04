// 📄 xbytechat-api/Features/CRM/Summary/Dtos/ContactSummaryResponseDto.cs
using System;
using System.Collections.Generic;
using xbytechat.api.Features.CRM.Timelines.DTOs;

namespace xbytechat.api.Features.CRM.Dtos
{
    /// <summary>
    /// Compact CRM snapshot for a contact:
    /// - Core contact fields
    /// - Tags
    /// - Recent notes
    /// - Next reminder
    /// - Recent timeline events
    /// </summary>
    public sealed class ContactSummaryResponseDto
    {
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }

        // Core contact profile
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? LeadSource { get; set; }

        public bool IsFavorite { get; set; }
        public bool IsArchived { get; set; }
        public string? Group { get; set; }

        public DateTime? LastContactedAt { get; set; }
        public DateTime? NextFollowUpAt { get; set; }

        // Structured tags (from ContactDto.ContactTags → ContactTagDto)
        public List<ContactTagDto> Tags { get; set; } = new();

        // Mini timeline section
        public List<NoteDto> RecentNotes { get; set; } = new();

        public ReminderDto? NextReminder { get; set; }

        public List<LeadTimelineDto> RecentTimeline { get; set; } = new();
    }
}
