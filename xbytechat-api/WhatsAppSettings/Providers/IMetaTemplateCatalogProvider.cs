using xbytechat.api.WhatsAppSettings.Abstractions;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.WhatsAppSettings.Providers
{
    public interface IMetaTemplateCatalogProvider
    {
        Task<IReadOnlyList<TemplateCatalogItem>> ListMetaAsync(
           WhatsAppSettingEntity setting,
           CancellationToken ct = default);

        Task<TemplateCatalogItem?> GetByNameMetaAsync(
            WhatsAppSettingEntity setting,
            string templateName,
            CancellationToken ct = default);
    }
}
