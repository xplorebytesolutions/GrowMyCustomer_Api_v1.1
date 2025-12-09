namespace xbytechat.api.Features.Webhooks.Models
{
    public class WebhookSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool AutoCleanupEnabled { get; set; } = true;
        public DateTime? LastCleanupAt { get; set; }
    }
}
