using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;      // TemplateCatalogItem
using xbytechat_api.WhatsAppSettings.Models;    // WhatsAppSettingEntity

namespace xbytechat.api.WhatsAppSettings.Abstractions
{
    public interface ITemplateCatalogProvider
    {
        Task<IReadOnlyList<TemplateCatalogItem>> ListAsync(
            WhatsAppSettingEntity setting,
            CancellationToken ct = default);

        Task<TemplateCatalogItem?> GetByNameAsync(
            WhatsAppSettingEntity setting,
            string templateName,
            CancellationToken ct = default);
    }
}
