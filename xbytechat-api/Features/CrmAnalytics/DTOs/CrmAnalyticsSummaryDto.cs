namespace xbytechat.api.Features.CrmAnalytics.DTOs
{
    public class CrmAnalyticsSummaryDto
    {
        public int TotalContacts { get; set; }
        public int TaggedContacts { get; set; }
        public int ActiveReminders { get; set; }
        public int CompletedReminders { get; set; }
        public int TotalNotes { get; set; }
        public int LeadsWithTimeline { get; set; }
        public int NewContactsToday { get; set; }
        public int NotesAddedToday { get; set; }
        public DateTime? LastContactAddedAt { get; set; }
        public DateTime? LastReminderCompletedAt { get; set; }
    }
}

