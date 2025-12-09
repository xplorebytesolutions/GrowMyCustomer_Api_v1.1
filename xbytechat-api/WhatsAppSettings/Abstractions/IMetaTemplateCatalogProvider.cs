using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.WhatsAppSettings.Abstractions
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
