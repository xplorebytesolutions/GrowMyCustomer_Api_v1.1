namespace xbytechat.api.Features.Inbox.Models
{
    public class ChatSessionState
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }

        public string Mode { get; set; } = "automation"; // values: "automation" | "agent"
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        // Optional: track who switched the mode
        public string? UpdatedBy { get; set; }
    }
}
