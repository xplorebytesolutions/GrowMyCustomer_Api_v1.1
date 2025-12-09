using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.WhatsAppSettings.Abstractions
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
