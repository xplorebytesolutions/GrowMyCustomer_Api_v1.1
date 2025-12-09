using System;

namespace xbytechat.api.Features.Inbox.Models
{
    public class ContactRead
    {
        public Guid Id { get; set; }

        // 🔗 FK to Contact
        public Guid ContactId { get; set; }

        // 🔗 FK to User (Agent)
        public Guid UserId { get; set; }

        // 📅 Last time this agent opened this contact's chat
        public DateTime LastReadAt { get; set; } = DateTime.UtcNow;

        public Guid BusinessId { get; set; }
    }
}
