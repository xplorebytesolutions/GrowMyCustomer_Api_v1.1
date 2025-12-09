using System;

namespace xbytechat.api.Features.CRM.Timelines.DTOs
{
    public class CRMTimelineLogDto
    {
        public Guid ContactId { get; set; }
        public Guid BusinessId { get; set; }
        public string EventType { get; set; }  // 🧩 Example: "NoteAdded", "ReminderSet", "TagApplied"
        public string Description { get; set; }
        public Guid? ReferenceId { get; set; }  // 🆔 Related NoteId, ReminderId, TagId (optional)
        public string CreatedBy { get; set; }
        public string? Category { get; set; } = "CRM";  // 📂 Default category: CRM
        public DateTime? Timestamp { get; set; }  // ⏰ Custom time if needed (else CreatedAt = now)
    }
}
