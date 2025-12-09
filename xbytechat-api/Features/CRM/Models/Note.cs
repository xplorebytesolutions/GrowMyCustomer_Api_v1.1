namespace xbytechat.api.Features.CRM.Models
{
    public class Note
    {
        public Guid Id { get; set; }

        // 🔗 Ownership & Association
        public Guid? BusinessId { get; set; }
        public Guid? ContactId { get; set; }

        // 📝 Core Content
        public string Title { get; set; } // Optional short title (for pinning or preview)
        public string Content { get; set; }

        // 🔖 Contextual Intelligence
        public string Source { get; set; } // e.g., "Manual", "Call Log", "WhatsApp", "LeadForm"
        public string CreatedBy { get; set; } // Store agent/user name or userId

        // 📌 UX Flags
        public bool IsPinned { get; set; } = false;
        public bool IsInternal { get; set; } = false; // if true, only visible to team

        // 🕓 Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
    }
}