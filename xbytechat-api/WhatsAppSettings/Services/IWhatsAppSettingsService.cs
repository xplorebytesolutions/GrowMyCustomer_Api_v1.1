using System;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat_api.WhatsAppSettings.Services
{
    public interface IWhatsAppSettingsService
    {
        Task SaveOrUpdateSettingAsync(SaveWhatsAppSettingDto dto);
        Task<WhatsAppSettingsDto> GetSettingsByBusinessIdAsync(Guid businessId);
        Task<bool> DeleteSettingsAsync(Guid businessId);
        Task<string> TestConnectionAsync(SaveWhatsAppSettingDto dto);
        Task<string?> GetSenderNumberAsync(Guid businessId);
        Task<string> GetCallbackUrlAsync(Guid businessId, string appBaseUrl);
        Task<IReadOnlyList<WhatsAppSettingEntity>> GetAllForBusinessAsync(Guid businessId);
        Task<WhatsAppSettingEntity?> GetSettingsByBusinessIdAndProviderAsync(Guid businessId, string provider);
      
    }
}
