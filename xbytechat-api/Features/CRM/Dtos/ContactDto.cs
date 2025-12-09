using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CRM.Dtos
{
    public class ContactDto
    {
        public Guid? Id { get; set; } // Nullable for Create (used in PUT)

        public string Name { get; set; } // Contact full name

        public string PhoneNumber { get; set; } // WhatsApp-compatible number

        public string? Email { get; set; } // Optional email address

        public string? LeadSource { get; set; } // e.g., "WhatsApp", "Facebook", "Landing Page"

        public DateTime? LastContactedAt { get; set; } // Last WhatsApp or CRM interaction

        public DateTime? NextFollowUpAt { get; set; } // For scheduling reminders

        public string? Notes { get; set; } // Internal notes for the contact

        public DateTime? CreatedAt { get; set; } // Read-only timestamp

        // ✅ NEW: Structured Tags (replaces comma-separated strings)
        // Example: [{ id: 1, name: "VIP" }, { id: 2, name: "Follow-up" }]
        public List<ContactTagDto> Tags { get; set; } = new();

        public bool IsFavorite { get; set; } = false;
        public bool IsArchived { get; set; } = false;
        public string? Group { get; set; }

        public bool IsTemporary { get; set; } = false;
        public Guid? SourceCampaignId { get; set; }
        public DateTime? ExpiresAt { get; set; }

    }
}
