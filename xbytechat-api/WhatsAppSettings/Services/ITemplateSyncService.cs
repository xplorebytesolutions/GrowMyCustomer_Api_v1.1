namespace xbytechat.api.WhatsAppSettings.Services
{
    public interface ITemplateSyncService
    {
        Task<TemplateSyncResult>
            SyncBusinessTemplatesAsync(Guid businessId, 
            bool force = false,
            bool onlyUpsert = false,
            CancellationToken ct = default);
    }
    public record TemplateSyncResult(int Added, int Updated, int Skipped, DateTime SyncedAt);

}
