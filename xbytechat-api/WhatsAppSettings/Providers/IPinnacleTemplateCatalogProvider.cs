using xbytechat.api.WhatsAppSettings.Abstractions;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.WhatsAppSettings.Providers
{
    public interface IPinnacleTemplateCatalogProvider
    {
        Task<IReadOnlyList<TemplateCatalogItem>> ListPinnacleAsync(
          WhatsAppSettingEntity setting,
          CancellationToken ct = default);

        Task<TemplateCatalogItem?> GetByNamePinnacleAsync(
            WhatsAppSettingEntity setting,
            string templateName,
            CancellationToken ct = default);
    }
}
