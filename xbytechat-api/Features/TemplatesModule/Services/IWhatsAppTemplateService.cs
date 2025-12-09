using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.Features.TemplateModule.Services
{
    public interface IWhatsAppTemplateService
    {
        Task<List<TemplateMetadataDto>> FetchTemplatesAsync();
    }
}
