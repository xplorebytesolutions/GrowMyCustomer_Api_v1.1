using xbytechat.api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.TemplateModule.Services
{
    public interface IWhatsAppTemplatesSyncBridge
    {
        Task<bool> SyncAfterActivationAsync(Guid businessId, CancellationToken ct = default);
    }

    public sealed class WhatsAppTemplatesSyncBridge : IWhatsAppTemplatesSyncBridge
    {
        private readonly ITemplateSyncService _sync;

        public WhatsAppTemplatesSyncBridge(ITemplateSyncService sync)
        {
            _sync = sync;
        }

        public async Task<bool> SyncAfterActivationAsync(Guid businessId, CancellationToken ct = default)
        {
            // Force = true (ignore TTL); onlyUpsert = true (don’t deactivate others)
            var result = await _sync.SyncBusinessTemplatesAsync(
                businessId,
                force: true,
                onlyUpsert: true,
                ct);

            // Consider any change or a successful “no-op” a success here
            return result is not null;
        }
    }
}
