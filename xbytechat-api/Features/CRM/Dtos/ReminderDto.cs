using System;

namespace xbytechat.api.Features.CRM.Dtos
{
    public class ReminderDto
    {
        public Guid? Id { get; set; }  // Null when creating, present when updating

        public Guid? ContactId { get; set; }

        public string Title { get; set; } = default!;

        public string? Description { get; set; }

        public DateTime DueAt { get; set; }

        public string? Status { get; set; } = "Pending";

        public string? ReminderType { get; set; }

        public int? Priority { get; set; }

        public bool IsRecurring { get; set; }

        public string? RecurrencePattern { get; set; }

        public bool SendWhatsappNotification { get; set; }

        public string? LinkedCampaign { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
