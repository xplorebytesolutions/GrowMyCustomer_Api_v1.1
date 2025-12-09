using System;

namespace xbytechat.api.Features.CRM.Models
{
    public class Reminder
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }   // For multi-tenant isolation

        public Guid ContactId { get; set; }    // Which contact this reminder is for

        public string Title { get; set; } = default!; // Main reminder title (e.g., "Call back about invoice")

        public string? Description { get; set; } // Longer notes, optional (for internal detail)

        public DateTime DueAt { get; set; }    // When reminder should notify

        public string Status { get; set; } = "Pending"; // "Pending", "Done", "Overdue"

        public string? ReminderType { get; set; } // e.g., "Call", "Email", "Follow-up", "Meeting"

        public int? Priority { get; set; } // e.g., 1 (High), 2 (Medium), 3 (Low)

        public bool IsRecurring { get; set; } = false; // For future → repeat reminder

        public string? RecurrencePattern { get; set; } // e.g., "Weekly", "Monthly" (optional)

        public bool SendWhatsappNotification { get; set; } = false; // Future: auto-WA message trigger

        public string? LinkedCampaign { get; set; } // Optional: which campaign this reminder relates to

        public bool IsActive { get; set; } = true;  // Soft delete support

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; } // Track when it was marked Done

        public string? LastCTAType { get; set; } // e.g., Confirm, Reschedule
        public DateTime? LastClickedAt { get; set; }
        public bool FollowUpSent { get; set; } = false;

    }
}

