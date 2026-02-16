namespace xbytechat.api.Features.Inbox.Models
{
    public class ChatSessionState
    {
        public const string ModeAutomation = "automation";
        public const string ModeAgent = "agent";

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }

        public string Mode { get; set; } = ModeAutomation; // values: "automation" | "agent"
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        // Optional: track who switched the mode
        public string? UpdatedBy { get; set; }

        public static string NormalizeMode(string? mode)
        {
            var raw = (mode ?? string.Empty).Trim().ToLowerInvariant();
            if (raw == "agent")
            {
                return ModeAgent;
            }

            if (raw == "auto" || raw == "automation")
            {
                return ModeAutomation;
            }

            return ModeAutomation;
        }
    }
}
